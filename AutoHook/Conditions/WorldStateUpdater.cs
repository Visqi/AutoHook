using Dalamud.Hooking;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Event;
using FFXIVClientStructs.FFXIV.Client.Game.InstanceContent;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.Game.WKS;

namespace AutoHook.Conditions;

public readonly struct BiteContext
{
    public double BiteTimeSeconds { get; init; }
    public bool ChumActive { get; init; }
    public IntuitionStatus IntuitionStatus { get; init; }
    public float IntuitionTimeRemaining { get; init; }
    public SpectralCurrentStatus SpectralCurrentStatus { get; init; }
    public int? LastCaughtFishId { get; init; }
}

public sealed class WorldStateUpdater : IDisposable
{
    private readonly Hook<ActionManager.Delegates.UseAction>? _useActionHook;

    public delegate void UpdateCatchDelegate(IntPtr module, uint fishId, bool large, ushort size, byte amount,
        byte level, byte unk7, byte unk8, byte unk9, byte unk10, byte unk11, byte unk12);
    private readonly Hook<UpdateCatchDelegate>? _updateCatchHook;

    public unsafe WorldStateUpdater()
    {
        _updateCatchHook = Svc.Hook.HookFromSignature<UpdateCatchDelegate>(SignaturePatterns.UpdateCatch, UpdateCatchDetour);
        _useActionHook = Svc.Hook.HookFromAddress<ActionManager.Delegates.UseAction>((nint)ActionManager.MemberFunctionPointers.UseAction, UseActionDetour);
        _updateCatchHook?.Enable();
        _useActionHook?.Enable();
    }

    public void Dispose()
    {
        _useActionHook?.Dispose();
        _updateCatchHook?.Dispose();
    }

    /// <summary>
    /// Push current game state into WorldState. Call every frame
    /// </summary>
    public void Update()
    {
        var ws = Service.WorldState;
        ws.Execute(CollectGp());
        ws.Execute(CollectStatuses());
        ws.Execute(CollectOceanFishing());

        var biteContext = CollectBiteContext(ws);

        ws.Execute(CollectFishingState(biteContext));
        ws.Execute(CollectActionAvailability());
        ws.Execute(CollectItemCounts());
        ws.Execute(CollectSwimbaitIds());
        ws.Execute(CollectPotCooldown());

        ws.Execute(new WorldState.OpBiteContext(biteContext.BiteTimeSeconds, biteContext.ChumActive));
        ws.Execute(new WorldState.OpIntuition(biteContext.IntuitionStatus, biteContext.IntuitionTimeRemaining));
        ws.Execute(new WorldState.OpLastCatch(biteContext.LastCaughtFishId));
    }

    private static BiteContext CollectBiteContext(WorldState ws)
    {
        return new BiteContext
        {
            BiteTimeSeconds = ws.BiteTimeSeconds,
            ChumActive = ws.HasStatus(IDs.Status.Chum),
            IntuitionStatus = ws.HasStatus(IDs.Status.FishersIntuition) ? IntuitionStatus.Active : IntuitionStatus.NotActive,
            IntuitionTimeRemaining = ws.GetStatusTime(IDs.Status.FishersIntuition),
            SpectralCurrentStatus = ws.SpectralCurrentStatus,
            LastCaughtFishId = ws.LastCaughtFishId,
        };
    }

    private static unsafe WorldState.OpOceanFishing CollectOceanFishing()
    {
        try
        {
            var ptr = EventFramework.Instance()->GetInstanceContentOceanFishing();
            if (ptr == null)
                return new WorldState.OpOceanFishing(null);

            var fishData = new List<InstanceContentOceanFishing.FishDataStruct>(60);
            foreach (var f in ptr->FirstZoneFishData)
                fishData.Add(f);
            foreach (var f in ptr->SecondZoneFishData)
                fishData.Add(f);
            foreach (var f in ptr->ThirdZoneFishData)
                fishData.Add(f);

            var state = new OceanFishingState
            {
                SpectralCurrentActive = ptr->SpectralCurrentActive,
                CurrentRoute = ptr->CurrentRoute,
                CurrentZone = ptr->CurrentZone,
                Mission1Type = ptr->Mission1Type,
                Mission2Type = ptr->Mission2Type,
                Mission3Type = ptr->Mission3Type,
                Mission1Progress = ptr->Mission1Progress,
                Mission2Progress = ptr->Mission2Progress,
                Mission3Progress = ptr->Mission3Progress,
                FishData = fishData,
            };
            return new WorldState.OpOceanFishing(state);
        }
        catch
        {
            return new WorldState.OpOceanFishing(null);
        }
    }

    private unsafe bool UseActionDetour(ActionManager* thisPtr, ActionType actionType, uint actionId, ulong targetId, uint extraParam, ActionManager.UseActionMode mode, uint comboRouteId, bool* outOptAreaTargeted)
    {
        try
        {
            if (actionType == ActionType.Action && Service.Configuration.PluginEnabled && Service.WorldState.ActionAvailable(actionId))
                Service.WorldState.Execute(new WorldState.OpPlayerUsedAction(actionType, actionId));
        }
        catch (Exception e)
        {
            Service.PrintDebug($"[WorldStateUpdater] UseAction: {e.Message}");
        }
        return _useActionHook!.Original(thisPtr, actionType, actionId, targetId, extraParam, mode, comboRouteId, outOptAreaTargeted);
    }

    private void UpdateCatchDetour(IntPtr module, uint fishId, bool large, ushort size, byte amount, byte level,
        byte unk7, byte unk8, byte unk9, byte unk10, byte unk11, byte unk12)
    {
        _updateCatchHook!.Original(module, fishId, large, size, amount, level, unk7, unk8, unk9, unk10, unk11, unk12);
        Service.WorldState.Execute(new WorldState.OpLastCatch((int?)ItemUtil.GetBaseId(fishId).ItemId, amount));
        Service.WorldState.Execute(new WorldState.OpSetFishingStep(FishingSteps.FishCaught));
    }

    public static int ComputeCurrentBaitMoochId(uint currentId, uint? swimbaitId, bool isMooching, BiteContext biteContext)
    {
        if (swimbaitId.HasValue && swimbaitId.Value != 0)
            return (int)swimbaitId.Value;
        if (GameRes.Fishes.Any(f => f.Id == currentId))
            return (int)currentId;
        if (isMooching && biteContext.LastCaughtFishId is { } lastId && lastId > 0 &&
            GameRes.Fishes.Any(f => f.Id == lastId))
            return lastId;
        return (int)currentId;
    }

    private static WorldState.OpGp CollectGp()
        => new(Svc.Objects.LocalPlayer?.CurrentGp ?? 0, Svc.Objects.LocalPlayer?.MaxGp ?? 0);

    private static WorldState.OpStatuses CollectStatuses()
    {
        var dict = new Dictionary<uint, (float Time, int Stacks)>();
        if (Svc.Objects.LocalPlayer is { StatusList: var statuses })
            foreach (var buff in statuses)
                dict[buff.StatusId] = (buff.RemainingTime, buff.Param);
        return new WorldState.OpStatuses(dict);
    }

    private static unsafe WorldState.OpFishingState CollectFishingState(BiteContext biteContext)
    {
        var state = FishingState.None;
        uint baitId = 0;
        uint? swimbaitId = null;
        var isMooching = false;
        var baitMoochId = 0;

        try
        {
            if (Player.Territory is { Value.TerritoryIntendedUse.RowId: 60 })
            {
                if (WKSManager.Instance() is not null and var cosmic)
                    baitId = cosmic->FishingBait;
            }
            else
                baitId = PlayerState.Instance()->FishingBait;

            var ef = EventFramework.Instance();
            var handler = ef != null ? ef->EventHandlerModule.FishingEventHandler : null;
            if (handler != null)
            {
                state = handler->State;
                if (handler->CurrentSelectedSwimBait is >= 0 and < 3)
                    swimbaitId = handler->SwimBaitItemIds[handler->CurrentSelectedSwimBait];
                isMooching = GameRes.MoochableFish.Any(f => f.Id == (int)baitId);
            }

            baitMoochId = ComputeCurrentBaitMoochId(baitId, swimbaitId, isMooching, biteContext);
        }
        catch { }

        return new WorldState.OpFishingState(state, baitId, swimbaitId, isMooching, baitMoochId);
    }

    private static WorldState.OpActionAvailability CollectActionAvailability()
    {
        var available = new List<(uint Id, ActionType Type)>();
        bool Add(uint id, ActionType actionType = ActionType.Action)
        {
            if (ActionTypeAvailable(id, actionType))
            {
                available.Add((id, actionType));
                return true;
            }
            return false;
        }
        Add(IDs.Actions.Mooch);
        Add(IDs.Actions.Mooch2);
        Add(IDs.Actions.MultiHook, ActionType.EventAction);
        Add(IDs.Actions.SurfaceSlap);
        Add(IDs.Actions.IdenticalCast);
        Add(IDs.Actions.Patience);
        Add(IDs.Actions.Patience2);
        Add(IDs.Actions.DoubleHook);
        Add(IDs.Actions.TripleHook);
        Add(IDs.Actions.StellarHook);
        Add(IDs.Actions.PrecisionHS);
        Add(IDs.Actions.PowerfulHS);
        Add(IDs.Actions.AmbitiousLure);
        Add(IDs.Actions.ModestLure);
        Add(IDs.Actions.Chum);
        Add(IDs.Actions.FishEyes);
        Add(IDs.Actions.PrizeCatch);
        Add(IDs.Actions.SparefulHand);
        Add(IDs.Actions.Collect);
        Add(IDs.Actions.BigGameFishing);
        Add(IDs.Actions.Cast);
        Add(IDs.Actions.Hook);
        Add(IDs.Actions.Rest);
        Add(IDs.Actions.Quit);
        Add(IDs.Item.Cordial, ActionType.Item);
        Add(IDs.Item.HQCordial, ActionType.Item);
        Add(IDs.Item.HiCordial, ActionType.Item);
        Add(IDs.Item.WateredCordial, ActionType.Item);
        Add(IDs.Item.HQWateredCordial, ActionType.Item);
        return new WorldState.OpActionAvailability(available);
    }

    private static unsafe bool ActionTypeAvailable(uint id, ActionType actionType)
    {
        if (ActionManager.Instance()->GetActionStatus(actionType, id) != 0)
            return false;
        var group = ActionManager.Instance()->GetRecastGroup((int)actionType, id);
        if (group == -1) return true;
        var detail = ActionManager.Instance()->GetRecastGroupDetail(group);
        return detail->Total - detail->Elapsed <= 0;
    }

    private static unsafe WorldState.OpItemCounts CollectItemCounts()
    {
        var dict = new Dictionary<uint, int>();
        try
        {
            var inv = InventoryManager.Instance();
            if (inv != null)
            {
                for (var i = 0; i < 4; i++)
                {
                    var container = inv->GetInventoryContainer((InventoryType)i);
                    if (container == null) continue;
                    for (var k = 0; k < container->Size; k++)
                    {
                        var slot = container->GetInventorySlot(k);
                        if (slot == null || slot->ItemId == 0) continue;
                        var id = slot->ItemId;
                        dict[id] = dict.GetValueOrDefault(id, 0) + slot->Quantity;
                    }
                }
            }
        }
        catch { }
        return new WorldState.OpItemCounts(dict);
    }

    private static unsafe WorldState.OpSwimbaitIds CollectSwimbaitIds()
    {
        var list = new List<uint>();
        try
        {
            if (EventFramework.Instance() is not null and var ef && ef->EventHandlerModule.FishingEventHandler is not null and var handler)
                list.Add(handler->SwimBaitItemIds.ToArray());
        }
        catch { }
        return new WorldState.OpSwimbaitIds(list);
    }

    private static unsafe WorldState.OpPotCooldown CollectPotCooldown()
    {
        var off = false;
        try
        {
            var recast = ActionManager.Instance()->GetRecastGroupDetail(68);
            off = recast->Total - recast->Elapsed <= 0;
        }
        catch { }
        return new WorldState.OpPotCooldown(off);
    }
}
