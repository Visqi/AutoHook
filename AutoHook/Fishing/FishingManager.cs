using AutoHook.Conditions;
using AutoHook.Data;
using AutoHook.Tasks;
using Dalamud.Plugin.Services;
using ECommons.Throttlers;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Event;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using System.Diagnostics;

namespace AutoHook.Fishing;

public partial class FishingManager : IDisposable {
    private const uint FisherJobId = 18;

    // todo: refactor this entire class
    private static readonly FishingPresets Presets = Service.Configuration.HookPresets;
    private double _timeout;
    private readonly Stopwatch _fishingTimer = new();
    private readonly EventSubscriptions _oceanEventSubs;

    private static WorldState Ws => Service.WorldState;

    public FishingManager() {
        _oceanEventSubs = new(Ws.OceanZoneStarted.Subscribe(OnOceanZoneStarted));
        try {
            Svc.Framework.Update += OnFrameworkUpdate;
            Svc.Chat.ChatMessage += OnMessageDelegate;
            Ws.Modified += OnWorldStateModified;
        }
        catch (Exception e) {
            Svc.Log.Error(@$"{e.Message}");
        }
    }

    public void Dispose() {
        _oceanEventSubs.Dispose();
        Svc.Framework.Update -= OnFrameworkUpdate;
        Svc.Chat.ChatMessage -= OnMessageDelegate;
        Ws.Modified -= OnWorldStateModified;
    }

    private void OnOceanZoneStarted(WorldState.OpOceanZoneStarted op) {
        var ocean = Ws.OceanFishing;
        Service.PrintDebug($"[AutoOceanFish] OnZoneStarted zone={op.ZoneIndex + 1}, {OceanStopUtil.FormatStateLog(ocean)}");

        if (!Service.Configuration.PluginEnabled) {
            Service.PrintDebug("[AutoOceanFish] Task not started: plugin disabled");
            return;
        }

        if (!Service.Configuration.AutoOceanFish) {
            Service.PrintDebug("[AutoOceanFish] Task not started: Auto ocean fishing disabled in Settings");
            return;
        }

        if (Svc.Automation.CurrentTask is AutoOceanFish existing) {
            Service.PrintDebug($"[AutoOceanFish] Task not started: AutoOceanFish already running (zone {existing.ZoneIndex + 1})");
            return;
        }

        Svc.Automation.Start(new AutoOceanFish(this, op.ZoneIndex));
        Service.PrintDebug($"[AutoOceanFish] Task started for zone {op.ZoneIndex + 1}");
    }

    private void OnWorldStateModified(WorldState.Operation op) {
        if (!Service.Configuration.PluginEnabled)
            return;

        switch (op) {
            case FishingInfo.OpPlayerUsedAction(var ua):
                if (ua.ActionType == ActionType.Action && Ws.ActionAvailable(ua.ActionId, ua.ActionType)) {
                    switch (ua.ActionId) {
                        case IDs.Actions.Rest:
                            if (Ws.Player.HasStatus(IDs.Status.CollectorsGlove))
                                AnimationCancel();
                            Ws.Execute(new FishingInfo.OpSetFishingStep(FishingSteps.Reeling));
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
                break;
            case FishingInfo.OpSetLastCatch:
                OnCatch();
                break;
        }
    }

    public void StartFishing() {
        if (!(Ws.ActionAvailable(IDs.Actions.Cast, ActionType.Action) && !Ws.Player.BlockCasting)) {
            Service.PrintChat(@"[AutoHook] You can't cast right now.");
            return;
        }

        TryApplyOceanFishingPreset();

        var extraCfg = GetExtraCfg();
        if (extraCfg is { ForceBaitSwap: true, Enabled: true }) {
            var result = ChangeBait((uint)extraCfg.ForcedBaitId);

            if (result == ChangeBaitReturn.Success) {
                Service.PrintChat(@$"[AutoHook] Starting with bait: {MultiString.GetItemName(extraCfg.ForcedBaitId)}");
                Service.Save();
            }
        }

        Ws.Execute(new FishingInfo.OpSetFishingStep(FishingSteps.StartedCasting));
        UseAutoCastsAfterStartFishing();
    }

    // The current config is updates two times: When we began fishing (to get the config based on the mooch/bait) and when we hooked the fish (in case the user updated their configs).
    private unsafe void UpdateStatusAndTimer() {
        if (Service.Configuration.ResetAfkTimer)
            InputTimerModule.Instance()->ResetAfkTimer();

        var selected = GetHookCfg();
        var hookset = selected.GetHookset();
        _timeout = selected.Enabled ? Ws.HasStatus(IDs.Status.Chum) ? hookset.ChumTimeoutMax : hookset.TimeoutMax : 0;

        if (Service.Configuration.ShowStatus) {
            var buffStatus = "";

            if (hookset.RequiredStatus != 0) {
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

    public string GetPresetName() {
        var bait = Ws.Fishing.BaitInfo;
        var currentBaitId = bait.SelectedSwimbaitId is { } sb ? (int)sb : bait.MoochId;
        var isMooching = bait.IsMooching;

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

    public HookConfig GetHookCfg() {
        var bait = Ws.Fishing.BaitInfo;
        var currentBaitId = bait.SelectedSwimbaitId is { } sb ? (int)sb : bait.MoochId;
        var isMooching = bait.IsMooching;

        HookConfig? custom = null;
        if (Presets.SelectedPreset != null)
            custom = Presets.SelectedPreset.GetCfgById(currentBaitId, isMooching);

        var defaultHook = isMooching
            ? Presets.DefaultPreset.ListOfMooch.FirstOrDefault()
            : Presets.DefaultPreset.ListOfBaits.FirstOrDefault();

        var currentHook = custom?.Enabled ?? false ? custom : defaultHook!;

        return currentHook;
    }

    private void OnFrameworkUpdate(IFramework _) {
        if (!Service.Configuration.PluginEnabled || !Svc.ClientState.IsLoggedIn || Svc.Objects.LocalPlayer == null)
            return;

        Service.WorldStateUpdater.Update();

        if (Player.ClassJob.RowId != FisherJobId) {
            SanitizeWorldStateWhenNotFisher();
            return;
        }

        var currentState = Service.WorldState.Fishing.FishingState;
        if (currentState == FishingState.None) {
            if (Service.Configuration.AutoStartFishing && !ShouldSuppressAutoStartFishing() && EzThrottler.Throttle("AutoStartFishing", 1000)) {
                var autoCastCfg = GetAutoCastCfg();
                if (autoCastCfg.EnableAll && autoCastCfg.CastLine.IsAvailableToCast() && Ws.IsCastAvailable()) {
                    StartFishing();
                }
            }

            return;
        }

        if (currentState != FishingState.Quitting && Ws.Fishing.FishingStep.HasFlag(FishingSteps.Quitting)) {
            if (Ws.ActionAvailable(IDs.Actions.Quit, ActionType.Action) && !Ws.Player.BlockCasting) {
                PlayerRes.CastActionDelayed(IDs.Actions.Quit, ActionType.Action, @"Quit");
                currentState = FishingState.Quitting;
            }
        }

        if (!Ws.Fishing.FishingStep.HasFlag(FishingSteps.Quitting) && currentState == FishingState.PoleReady)
            CheckPluginActions();

        if (currentState is FishingState.AmbitiousLure or FishingState.LineInWater) {
            CheckWhileFishingActions();
            CheckTimeout();
        }

        if (Ws.Fishing.PreviousFishingState == currentState)
            return;

        Ws.Execute(new FishingInfo.OpSetPreviousFishingState(currentState));

        switch (currentState) {
            case FishingState.PullingPoleIn:
                if (Ws.Fishing.FishingStep.HasFlag(FishingSteps.BeganFishing))
                    Ws.Execute(new FishingInfo.OpSetFishingStep(FishingSteps.None));
                else AnimationCancel();
                _fishingTimer.Reset();
                break;
            case FishingState.CastingOut:
                InitFinishing();
                break;
            case FishingState.Bite:
                Service.TaskManager.Enqueue(OnBite);
                break;
            case FishingState.Quitting:
                if (!Ws.FishingStep.HasFlag(FishingSteps.Quitting))
                    Ws.Execute(new FishingInfo.OpSetFishingStep(FishingSteps.Quitting));
                OnFishingStop();
                break;
        }
    }

    // ocean fishing handles it on its own
    private bool ShouldSuppressAutoStartFishing() => Service.Configuration.AutoOceanFish && (Svc.Automation.CurrentTask is AutoOceanFish || Ws.OceanFishing != OceanFishingState.Empty);

    /// <summary>
    /// When not on Fisher, <see cref="WorldStateUpdater"/> does not refresh fishing fields; clear stale automation state
    /// so gathering/other jobs are not blocked by leftover block-casting or fishing flags.
    /// </summary>
    private void SanitizeWorldStateWhenNotFisher() {
        var f = Ws.Fishing;
        if (!Ws.Player.BlockCasting
            && f.FishingState == FishingState.None
            && f.FishingStep == FishingSteps.None
            && f.PreviousFishingState == FishingState.None)
            return;

        Ws.Execute(new WorldState.OpSetBlockCasting(false));
        Ws.Execute(new FishingInfo.OpSetFishingStep(FishingSteps.None));
        Ws.Execute(new FishingInfo.OpSetPreviousFishingState(FishingState.None));
        Ws.Execute(new FishingInfo.OpFishingState(FishingState.None, new BaitInfo(0, null, 0, false)));
    }

    private void InitFinishing() {
        if (!_fishingTimer.IsRunning)
            _fishingTimer.Start();

        UpdateStatusAndTimer();
    }

    private void CheckPluginActions() {
        if (!EzThrottler.Throttle(@"CheckPluginActions", 500))
            return;

        QueueResolveCollectables(); // must run before anything that sets blockcasting

        if (!Ws.IsCastAvailable())
            return;

        if (Ws.Fishing.FishingStep.HasFlag(FishingSteps.FishCaught) &&
            (Ws.Fishing.FishingStep & (FishingSteps.None | FishingSteps.Quitting)) == 0)
            CheckStopCondition();

        CheckExtraActions();

        var lastCatchCfg = GetLastCatchConfig();

        var casted = false;
        if (Ws.FishingStep.HasFlag(FishingSteps.FishCaught) && !Ws.FishingStep.HasFlag(FishingSteps.Quitting)) {
            CheckFishCaughtSwap(lastCatchCfg);
            lastCatchCfg = GetLastCatchConfig();
            casted = UseFishCaughtActions(lastCatchCfg);
        }

        FishingHelper.RemoveGuidQueue();

        if (!casted)
            UseAutoCasts();
    }

    private void OnBeganFishing(bool mooching) {
        if (Ws.Fishing.FishingStep.HasFlag(FishingSteps.BeganFishing) &&
            Ws.Fishing.PreviousFishingState != FishingState.PoleReady && Ws.Fishing.PreviousFishingState != FishingState.None)
            return;

        Ws.Execute(new FishingInfo.OpSetLureSuccess(false));

        var baitname = MultiString.GetItemName(Ws.Fishing.BaitInfo.MoochId);
        if (!mooching)
            Service.PrintDebug(@$"Started fishing with normal bait: {baitname}");
        else
            Service.PrintDebug(@$"Started mooching/swimbait with {baitname}");

        Ws.Execute(new FishingInfo.OpSetFishingStep(FishingSteps.BeganFishing));

        Service.TaskManager.EnqueueDelay(2500);
        Service.TaskManager.Enqueue(CastCollect);

        UpdateStatusAndTimer();
    }

    private void CheckTimeout() {
        if (!_fishingTimer.IsRunning)
            _fishingTimer.Start();

        var maxTime = Math.Truncate(_timeout * 100) / 100;

        var currentTime = Math.Truncate(_fishingTimer.ElapsedMilliseconds / 1000.0 * 100) / 100;

        if (!(maxTime > 0) || !(currentTime > maxTime) || Ws.Fishing.FishingStep.HasFlag(FishingSteps.TimeOut) ||
            Ws.Fishing.FishingStep.HasFlag(FishingSteps.Reeling))
            return;

        Service.Status = @$"Timeout reached - using Rest";
        PlayerRes.CastActionDelayed(IDs.Actions.Rest, ActionType.Action, UIStrings.Hook);
        Ws.Execute(new FishingInfo.OpSetFishingStep(FishingSteps.TimeOut));
    }

    private void OnBite() {
        UpdateStatusAndTimer();
        var currentHook = GetHookCfg();
        _fishingTimer.Stop();

        if (Ws.Player.HasStatus(IDs.Status.Salvage) && GetAutoCastCfg().ChumAnimationCancel)
            PlayerRes.CastAction(IDs.Actions.Salvage);

        HookFish(Ws.Fishing.BiteInfo.TugType.ToBiteType(), currentHook);
    }

    private void HookFish(BiteType bite, HookConfig currentHook) {
        var delay = new Random().Next(Service.Configuration.DelayBetweenHookMin,
            Service.Configuration.DelayBetweenHookMax);

        if (!currentHook.Enabled)
            return;

        var timePassed = Math.Truncate(_fishingTimer.ElapsedMilliseconds / 1000.0 * 100) / 100;
        var ws = Service.WorldState;
        ws.Execute(new FishingInfo.OpBiteContext(timePassed, ws.Player.HasStatus(IDs.Status.Chum)));
        ws.Execute(new FishingInfo.OpIntuition(new IntuitionInfo(ws.Fishing.Intuition.Status, ws.Player.GetStatusTime(IDs.Status.FishersIntuition))));
        ws.Execute(new OceanFishInfo.OpOceanFishing(ws.Ocean.OceanFishing));

        var hook = currentHook.GetHook(bite, timePassed);

        if (hook is null or HookType.None) {
            delay = new Random().Next(Service.Configuration.DelayBeforeCancelMin,
                Service.Configuration.DelayBeforeCancelMax);

            Service.TaskManager.EnqueueDelay(delay);
            Service.TaskManager.Enqueue(() => PlayerRes.CastAction(IDs.Actions.Rest));
            //_lastStep = FishingSteps.Reeling;
            Service.PrintDebug(@$"[HookManager] No hook found, using Rest");
            return;
        }

        Service.TaskManager.EnqueueDelay(delay);
        Service.TaskManager.Enqueue(() => PlayerRes.CastActionDelayed((uint)hook, ActionType.Action, @$"{hook}"));
        Service.Status = @$"Using {hook} hook. (Bite: {bite})";
    }

    private void OnCatch() {
        if (Ws.Fishing.LastCatch is not { } lastCatch || lastCatch.FishId <= 0 || lastCatch.Amount == 0)
            return;

        var fishId = lastCatch.FishId;
        var amount = lastCatch.Amount;
        var lastCatchFish = GameRes.Fishes.FirstOrDefault(fish => fish.Id == fishId) ?? new BaitFishClass(@"-", -1);
        Ws.Execute(new FishingInfo.OpAddFishCaught(fishId, amount));
        var lastFishCatchCfg = GetLastCatchConfig();
        var currentHook = GetHookCfg();

        Service.LastCatch = lastCatchFish;

        Service.PrintDebug(@$"[HookManager] Caught {lastCatchFish.Name} (id {lastCatchFish.Id})");

        if (lastFishCatchCfg != null) {
            for (var i = 0; i < amount; i++) {
                FishingHelper.AddFishCount(lastFishCatchCfg.UniqueId);
            }

            Service.NotificationMaster.TryNotify(lastFishCatchCfg.NotifyOnSuccess with { ToastText = $"Caught {lastCatchFish.Name} x{amount}" });
        }

        if (currentHook.Enabled) {
            FishingHelper.AddFishCount(currentHook.UniqueId);
            Service.NotificationMaster.TryNotify(currentHook.NotifyOnSuccess with { ToastText = $"Hook success with {currentHook.BaitFish.Name}: {lastCatchFish.Name} x{amount}" });
        }
    }

    private void CheckStopCondition() {
        var lastFishCatchCfg = GetLastCatchConfig();
        var currentHook = GetHookCfg();
        var hookset = currentHook.GetHookset();

        // Per-fish "Stop After Caught" logic - only active when explicitly enabled in the UI
        if (lastFishCatchCfg is not null) {
            var (stopEnabled, _) = lastFishCatchCfg.StopAfterCaughtLimit.Value;

            if (stopEnabled && lastFishCatchCfg.StopAfterCaughtLimit.BackingSet is { Groups.Count: > 0 } stopSet) {
                var shouldStop = stopSet.Evaluate(Ws, ConditionRegistry.Registry);
                if (shouldStop) {
                    var (_, limit) = lastFishCatchCfg.StopAfterCaughtLimit.Value;
                    Service.PrintChat(string.Format(UIStrings.Caught_Limited_Reached_Chat_Message,
                        @$"{lastFishCatchCfg.Fish.Name}: {limit}"));

                    Ws.Execute(new FishingInfo.OpOrFishingStep(lastFishCatchCfg.StopFishingStep));
                    if (lastFishCatchCfg.StopAfterResetCount) FishingHelper.ToBeRemoved.Add(lastFishCatchCfg.UniqueId);
                }
            }
        }

        // Bait/mooch preset "Stop After Hooking" logic
        if (currentHook.Enabled && hookset.StopAfterCaught) {
            var guid = currentHook.UniqueId;
            var total = FishingHelper.GetFishCount(guid);

            if (total >= hookset.StopAfterCaughtLimit) {
                Service.PrintChat(string.Format(UIStrings.Hooking_Limited_Reached_Chat_Message,
                    @$"{currentHook.BaitFish.Name}: {hookset.StopAfterCaughtLimit}"));

                Ws.Execute(new FishingInfo.OpOrFishingStep(hookset.StopFishingStep));
                if (hookset.StopAfterResetCount) FishingHelper.ToBeRemoved.Add(guid);
            }
        }
    }

    private void OnFishingStop() {
        Ws.Execute(new FishingInfo.OpSetFishingStep(FishingSteps.None));
        Ws.Execute(new FishingInfo.OpResetFishCaught());
        Ws.Execute(new FishingInfo.OpClearSessionCatches());

        if (_fishingTimer.IsRunning)
            _fishingTimer.Reset();

        Service.Status = "";

        FishingHelper.Reset();

        PlayerRes.CastActionNoDelay(IDs.Actions.Quit);
        PlayerRes.DelayNextCast(0);
    }
}
