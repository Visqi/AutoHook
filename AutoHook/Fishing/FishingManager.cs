using AutoHook.Conditions;
using Dalamud.Hooking;
using Dalamud.Plugin.Services;
using ECommons.Throttlers;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Event;
using System.Diagnostics;

namespace AutoHook.Fishing;

public partial class FishingManager : IDisposable
{
    // todo: refactor this entire class
    private static readonly FishingPresets Presets = Service.Configuration.HookPresets;

    private double _timeout;
    private readonly Stopwatch _fishingTimer = new();

    private static WorldState Ws => Service.WorldState;

    private delegate bool UseActionDelegate(IntPtr manager, ActionType actionType, uint actionId, ulong targetId,
        uint a4, uint a5,
        uint a6, IntPtr a7);

    private Hook<UseActionDelegate>? _useActionHook;

    public delegate void UpdateCatchDelegate(IntPtr module, uint fishId, bool large, ushort size, byte amount,
        byte level, byte unk7, byte unk8, byte unk9, byte unk10,
        byte unk11, byte unk12);

    public Hook<UpdateCatchDelegate>? UpdateCatch = null!;

    public FishingManager()
    {
        try
        {
            Service.TaskManager.EnqueueDelay(200);
            Service.TaskManager.Enqueue(CreateDalamudHooks);
        }
        catch (Exception e)
        {
            Svc.Log.Error(@$"{e.Message}");
        }
    }

    public void Dispose()
    {
        Disable();
        _useActionHook?.Dispose();
        UpdateCatch?.Dispose();
    }

    public unsafe void CreateDalamudHooks()
    {
        UpdateCatch = Svc.Hook.HookFromSignature<UpdateCatchDelegate>(
            SignaturePatterns.UpdateCatch,
            UpdateCatchDetour);
        var hookPtr = (IntPtr)ActionManager.MemberFunctionPointers.UseAction;
        _useActionHook = Svc.Hook.HookFromAddress<UseActionDelegate>(hookPtr, OnUseAction);

        Enable();
    }

    private void Enable()
    {
        Svc.Framework.Update += OnFrameworkUpdate;
        Svc.Chat.CheckMessageHandled += OnMessageDelegate;
        UpdateCatch?.Enable();
        _useActionHook?.Enable();
    }

    private void Disable()
    {
        Svc.Framework.Update -= OnFrameworkUpdate;
        Svc.Chat.CheckMessageHandled -= OnMessageDelegate;
        _useActionHook?.Disable();
        UpdateCatch?.Disable();
    }

    public void StartFishing()
    {
        if (!Ws.IsCastAvailable())
        {
            Service.PrintChat(@"[AutoHook] You can't cast right now.");
            return;
        }

        var extraCfg = GetExtraCfg();
        if (extraCfg is { ForceBaitSwap: true, Enabled: true })
        {
            var result = Service.BaitManager.ChangeBait((uint)extraCfg.ForcedBaitId);

            if (result == BaitManager.ChangeBaitReturn.Success)
            {
                Service.PrintChat(
                    @$"[AutoHook] Starting with bait: {MultiString.GetItemName(extraCfg.ForcedBaitId)}");
                Service.Save();
            }
        }

        Ws.Execute(new WorldState.OpSetFishingStep(FishingSteps.StartedCasting));
        UseAutoCasts();
        //Service.TaskManager.Enqueue(() => UseAutoCasts());
    }

    // The current config is updates two times: When we began fishing (to get the config based on the mooch/bait) and when we hooked the fish (in case the user updated their configs).
    private void UpdateStatusAndTimer()
    {
        ResetAfkTimer();

        var selected = GetHookCfg();
        var hookset = selected.GetHookset();
        if (selected.Enabled)
        {
            _timeout = Ws.HasStatus(IDs.Status.Chum)
                ? hookset.ChumTimeoutMax
                : hookset.TimeoutMax;
        }
        else
            _timeout = 0;

        if (Service.Configuration.ShowStatus)
        {
            var buffStatus = "";

            if (hookset.RequiredStatus != 0)
            {
                buffStatus = MultiString.GetStatusName(hookset.RequiredStatus);
                buffStatus = @$"({buffStatus})";
            }

            var hookCfgName = GetPresetName();

            var message = !selected.Enabled
                ? @$"No hooking option found. Make sure to add/enable your bait/mooch settings"
                : @$"Hooking with: {hookCfgName} {buffStatus}";

            Service.Status = message;
            Service.PrintDebug(@$"[HookManager] {message}");
        }
    }

    public string GetPresetName()
    {
        var isMooching = Ws.IsMooching || Ws.SessionIsMooching || Ws.CurrentSwimbaitId is { };
        var currentBaitId = Ws.CurrentSwimbaitId is { } sb ? (int)sb : WorldStateUpdater.ComputeCurrentBaitMoochId(Ws.CurrentBaitId, Ws.CurrentSwimbaitId, Ws.SessionIsMooching, new BiteContext { LastCaughtFishId = Ws.LastCaughtFishId });

        HookConfig? customHook = null;
        if (Presets.SelectedPreset != null)
            customHook = Presets.SelectedPreset.GetCfgById(currentBaitId, isMooching);

        var globalHook = isMooching
            ? Presets.DefaultPreset.ListOfMooch.FirstOrDefault()
            : Presets.DefaultPreset.ListOfBaits.FirstOrDefault();

        var presetName = customHook?.Enabled ?? false
            ? @$"{customHook.BaitFish.Name} ({Presets.SelectedPreset?.PresetName})"
            : globalHook?.Enabled ?? false
                ? @$"{(isMooching ? UIStrings.All_Mooches : UIStrings.All_Baits)} ({Presets.DefaultPreset.PresetName})"
                : @"None";

        return presetName;
    }

    public HookConfig GetHookCfg()
    {
        var isMooching = Ws.IsMooching || Ws.SessionIsMooching || Ws.CurrentSwimbaitId is { };
        var currentBaitId = Ws.CurrentSwimbaitId is { } sb ? (int)sb : WorldStateUpdater.ComputeCurrentBaitMoochId(Ws.CurrentBaitId, Ws.CurrentSwimbaitId, Ws.SessionIsMooching, new BiteContext { LastCaughtFishId = Ws.LastCaughtFishId });

        HookConfig? custom = null;
        if (Presets.SelectedPreset != null)
            custom = Presets.SelectedPreset.GetCfgById(currentBaitId, isMooching);

        var defaultHook = isMooching
            ? Presets.DefaultPreset.ListOfMooch.FirstOrDefault()
            : Presets.DefaultPreset.ListOfBaits.FirstOrDefault();

        var currentHook = custom?.Enabled ?? false ? custom : defaultHook!;

        return currentHook;
    }

    private void OnFrameworkUpdate(IFramework _)
    {
        if (!Service.Configuration.PluginEnabled || !Svc.ClientState.IsLoggedIn || Svc.Objects.LocalPlayer == null || !Service.BaitManager.IsValid) return;

        var currentState = Service.WorldState.FishingState;
        if (currentState == FishingState.None)
        {
            if (Service.Configuration.AutoStartFishing && EzThrottler.Throttle("AutoStartFishing", 1000))
            {
                var autoCastCfg = GetAutoCastCfg();
                if (autoCastCfg.EnableAll && autoCastCfg.CastLine.IsAvailableToCast() && Ws.IsCastAvailable())
                {
                    StartFishing();
                }
            }
            return;
        }

        if (currentState != FishingState.Quitting && Ws.FishingStep.HasFlag(FishingSteps.Quitting))
        {
            if (Ws.IsCastAvailable())
            {
                PlayerRes.CastActionDelayed(IDs.Actions.Quit, ActionType.Action, @"Quit");
                currentState = FishingState.Quitting;
            }
        }

        if (!Ws.FishingStep.HasFlag(FishingSteps.Quitting) && currentState == FishingState.PoleReady)
            CheckPluginActions();

        if (currentState is FishingState.AmbitiousLure or FishingState.LineInWater)
        {
            CheckWhileFishingActions();
            CheckTimeout();
        }

        if (Ws.PreviousFishingState == currentState)
            return;

        Ws.Execute(new WorldState.OpSetPreviousFishingState(currentState));

        switch (currentState)
        {
            case FishingState.PullingPoleIn:
                if (Ws.FishingStep.HasFlag(FishingSteps.BeganFishing))
                    Ws.Execute(new WorldState.OpSetFishingStep(FishingSteps.None));
                else AnimationCancel();
                _fishingTimer.Reset();
                break;
            case FishingState.CastingOut:
                InitFinishing();
                break;
            case FishingState.Bite:
                if (!Ws.FishingStep.HasFlag(FishingSteps.FishBit)) Service.TaskManager.Enqueue(OnBite);
                break;
            case FishingState.Quitting:
                OnFishingStop();
                break;
        }
    }

    private void InitFinishing()
    {
        if (!_fishingTimer.IsRunning)
            _fishingTimer.Start();

        UpdateStatusAndTimer();
    }

    FishConfig? lastCatchCfg = null;

    private void CheckPluginActions()
    {
        if (!EzThrottler.Throttle(@"CheckPluginActions", 500))
            return;

        if (!Ws.IsCastAvailable())
            return;

        lastCatchCfg ??= GetLastCatchConfig();

        var extraCfg = GetExtraCfg();

        if (Ws.FishingStep.HasFlag(FishingSteps.FishCaught) &&
            (Ws.FishingStep & (FishingSteps.None | FishingSteps.Quitting)) == 0)
            CheckStopCondition();

        CheckExtraActions(extraCfg);

        var casted = false;
        if (Ws.FishingStep.HasFlag(FishingSteps.FishCaught) && !Ws.FishingStep.HasFlag(FishingSteps.Quitting))
        {
            casted = UseFishCaughtActions(lastCatchCfg);
            CheckFishCaughtSwap(lastCatchCfg);
        }

        FishingHelper.RemoveGuidQueue();

        if (!casted)
            UseAutoCasts();
    }

    private void OnBeganFishing(bool mooching)
    {
        if (Ws.FishingStep.HasFlag(FishingSteps.BeganFishing) &&
            (Ws.PreviousFishingState != FishingState.PoleReady && Ws.PreviousFishingState != FishingState.None))
            return;

        Ws.Execute(new WorldState.OpSetSessionIsMooching(mooching));
        Ws.Execute(new WorldState.OpSetLureSuccess(false));

        var baitname = MultiString.GetItemName(WorldStateUpdater.ComputeCurrentBaitMoochId(Ws.CurrentBaitId, Ws.CurrentSwimbaitId, mooching, new BiteContext { LastCaughtFishId = Ws.LastCaughtFishId }));
        if (!mooching)
            Service.PrintDebug(@$"Started fishing with {(Ws.IsMooching ? @"Swimbait/Mooch" : @"normal bait")}: {baitname}");
        else
            Service.PrintDebug(@$"Started mooching with {baitname}");

        Ws.Execute(new WorldState.OpSetFishingStep(FishingSteps.BeganFishing));
        lastCatchCfg = null;

        Service.TaskManager.EnqueueDelay(2500);
        Service.TaskManager.Enqueue(CastCollect);

        UpdateStatusAndTimer();
    }

    private void CheckTimeout()
    {
        if (!_fishingTimer.IsRunning)
            _fishingTimer.Start();

        var maxTime = Math.Truncate(_timeout * 100) / 100;

        var currentTime = Math.Truncate(_fishingTimer.ElapsedMilliseconds / 1000.0 * 100) / 100;

        if (!(maxTime > 0) || !(currentTime > maxTime) || Ws.FishingStep.HasFlag(FishingSteps.TimeOut) ||
            Ws.FishingStep.HasFlag(FishingSteps.Reeling))
            return;

        Service.Status = @$"Timeout reached - using Rest";
        PlayerRes.CastActionDelayed(IDs.Actions.Rest, ActionType.Action, UIStrings.Hook);
        Ws.Execute(new WorldState.OpSetFishingStep(FishingSteps.TimeOut));
    }

    private void OnBite()
    {
        UpdateStatusAndTimer();
        var currentHook = GetHookCfg();
        _fishingTimer.Stop();

        if (Ws.HasStatus(IDs.Status.Salvage) && GetAutoCastCfg().ChumAnimationCancel)
            PlayerRes.CastAction(IDs.Actions.Salvage);

        Ws.Execute(new WorldState.OpSetFishingStep(FishingSteps.FishBit));
        HookFish(Service.TugType?.Bite ?? BiteType.Unknown, currentHook);
    }

    private void HookFish(BiteType bite, HookConfig currentHook)
    {
        var delay = new Random().Next(Service.Configuration.DelayBetweenHookMin,
            Service.Configuration.DelayBetweenHookMax);

        if (!currentHook.Enabled)
            return;

        var timePassed = Math.Truncate(_fishingTimer.ElapsedMilliseconds / 1000.0 * 100) / 100;
        var ws = Service.WorldState;
        ws.Execute(new WorldState.OpBiteContext(timePassed, ws.HasStatus(IDs.Status.Chum)));
        ws.Execute(new WorldState.OpIntuition(ws.IntuitionStatus, ws.GetStatusTime(IDs.Status.FishersIntuition)));
        ws.Execute(new WorldState.OpOceanFishing(ws.OceanFishing));
        ws.Execute(new WorldState.OpLastCatch(ws.LastCaughtFishId));

        var hook = currentHook.GetHook(bite, timePassed);

        if (hook is null or HookType.None)
        {
            delay = new Random().Next(Service.Configuration.DelayBeforeCancelMin,
                Service.Configuration.DelayBeforeCancelMax);

            Service.TaskManager.EnqueueDelay(delay);
            Service.TaskManager.Enqueue(() => PlayerRes.CastAction(IDs.Actions.Rest));
            //_lastStep = FishingSteps.Reeling;
            Service.PrintDebug(@$"[HookManager] No hook found, using Rest");
            return;
        }

        Service.TaskManager.EnqueueDelay(delay);
        Service.TaskManager.Enqueue(() =>
            PlayerRes.CastActionDelayed((uint)hook, ActionType.Action, @$"{hook}"));
        Service.Status = (@$"Using {hook} hook. (Bite: {bite})");
    }

    private void OnCatch(uint fishId, uint amount)
    {
        var lastCatch = GameRes.Fishes.FirstOrDefault(fish => fish.Id == fishId) ?? new BaitFishClass(@"-", -1);
        Ws.Execute(new WorldState.OpLastCatch(lastCatch.Id, (byte)amount));
        var lastFishCatchCfg = GetLastCatchConfig();

        Service.LastCatch = lastCatch;

        Service.PrintDebug(@$"[HookManager] Caught {lastCatch.Name} (id {lastCatch.Id})");

        Ws.Execute(new WorldState.OpSetFishingStep(FishingSteps.FishCaught));

        if (lastFishCatchCfg != null)
        {
            for (var i = 0; i < amount; i++)
            {
                FishingHelper.AddFishCount(lastFishCatchCfg.UniqueId);
            }
        }

        var hook = GetHookCfg();
        if (hook.Enabled)
            FishingHelper.AddFishCount(hook.UniqueId);
    }

    private void CheckStopCondition()
    {
        var lastFishCatchCfg = GetLastCatchConfig();
        var currentHook = GetHookCfg();
        var hookset = currentHook.GetHookset();
        var extra = GetExtraCfg();

        if (lastFishCatchCfg?.StopAfterCaught ?? false)
        {
            var guid = lastFishCatchCfg.UniqueId;
            var total = FishingHelper.GetFishCount(guid);

            if (total >= lastFishCatchCfg.StopAfterCaughtLimit)
            {
                Service.PrintChat(string.Format(UIStrings.Caught_Limited_Reached_Chat_Message,
                    @$"{lastFishCatchCfg.Fish.Name}: {lastFishCatchCfg.StopAfterCaughtLimit}"));

                Ws.Execute(new WorldState.OpOrFishingStep(lastFishCatchCfg.StopFishingStep));
                if (lastFishCatchCfg.StopAfterResetCount) FishingHelper.ToBeRemoved.Add(guid);
            }
        }

        if (currentHook.Enabled && hookset.StopAfterCaught)
        {
            var guid = currentHook.UniqueId;
            var total = FishingHelper.GetFishCount(guid);

            if (total >= hookset.StopAfterCaughtLimit)
            {
                Service.PrintChat(string.Format(UIStrings.Hooking_Limited_Reached_Chat_Message,
                    @$"{currentHook.BaitFish.Name}: {hookset.StopAfterCaughtLimit}"));

                Ws.Execute(new WorldState.OpOrFishingStep(hookset.StopFishingStep));
                if (hookset.StopAfterResetCount) FishingHelper.ToBeRemoved.Add(guid);
            }
        }

        if (extra.StopAfterAnglersArt && extra.Enabled)
        {
            if (!Ws.HasAnglersArtStacks(extra.AnglerStackQtd))
                return;

            Ws.Execute(new WorldState.OpOrFishingStep(extra.AnglerStopFishingStep));
            Service.PrintChat(@$"[Extra] Angler's Stack Reached: Stopping fishing");
        }
    }

    private void OnFishingStop()
    {
        Ws.Execute(new WorldState.OpSetFishingStep(FishingSteps.None));

        if (_fishingTimer.IsRunning)
            _fishingTimer.Reset();

        Service.Status = "";

        FishingHelper.Reset();

        PlayerRes.CastActionNoDelay(IDs.Actions.Quit);
        PlayerRes.DelayNextCast(0);
    }

    private bool OnUseAction(IntPtr manager, ActionType actionType, uint actionId, ulong targetId, uint a4,
        uint a5, uint a6, IntPtr a7)
    {
        try
        {
            if (actionType == ActionType.Action && Service.Configuration.PluginEnabled &&
                Ws.ActionAvailable(actionId))
            {
                switch (actionId)
                {
                    case IDs.Actions.Rest:
                        if (Ws.HasStatus(IDs.Status.CollectorsGlove)) AnimationCancel();
                        Ws.Execute(new WorldState.OpSetFishingStep(FishingSteps.Reeling));
                        break;
                    case IDs.Actions.Cast:
                        OnBeganFishing(false);
                        break;
                    case IDs.Actions.Mooch:
                    case IDs.Actions.Mooch2:
                        OnBeganFishing(true);
                        break;
                }
            }
        }
        catch (Exception e)
        {
            Service.PrintDebug(@$"[HookManager] Error: {e.Message}");
        }

        return _useActionHook!.Original(manager, actionType, actionId, targetId, a4, a5, a6, a7);
    }

    private void UpdateCatchDetour(IntPtr module, uint fishId, bool large, ushort size, byte amount, byte level,
        byte unk7,
        byte unk8, byte unk9, byte unk10, byte unk11, byte unk12)
    {
        UpdateCatch!.Original(module, fishId, large, size, amount, level, unk7, unk8, unk9, unk10, unk11, unk12);

        // Check against collectibles.
        if (fishId > 500000)
            fishId -= 500000;

        OnCatch(fishId, amount);
    }
}
