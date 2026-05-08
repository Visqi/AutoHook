using Dalamud.Game.Inventory.InventoryEventArgTypes;
using Dalamud.Hooking;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Event;
using FFXIVClientStructs.FFXIV.Client.Game.InstanceContent;
using FFXIVClientStructs.FFXIV.Client.Game.WKS;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Lumina.Excel.Sheets;

namespace AutoHook;

public readonly struct BiteContext {
    public double BiteTimeSeconds { get; init; }
    public bool ChumActive { get; init; }
    public IntuitionStatus IntuitionStatus { get; init; }
    public float IntuitionTimeRemaining { get; init; }
    public SpectralCurrentStatus SpectralCurrentStatus { get; init; }
    public int? LastCaughtFishId { get; init; }
}

public sealed class WorldStateUpdater : IDisposable {
    private readonly Hook<ActionManager.Delegates.UseAction>? _useActionHook;
    private readonly Hook<AgentCatch.Delegates.UpdateCatch>? _updateCatchHook;
    private readonly Hook<FishingEventHandler.Delegates.PlayAnimation>? _playAnimationHook;
    private static IReadOnlyList<Lumina.Excel.Sheets.Action> FshActions = [];

    private bool _needInventoryUpdate = true;

    public unsafe WorldStateUpdater() {
        _updateCatchHook = Svc.Hook.HookFromAddress<AgentCatch.Delegates.UpdateCatch>((nint)AgentCatch.MemberFunctionPointers.UpdateCatch, UpdateCatchDetour);
        _useActionHook = Svc.Hook.HookFromAddress<ActionManager.Delegates.UseAction>((nint)ActionManager.MemberFunctionPointers.UseAction, UseActionDetour);
        _playAnimationHook = Svc.Hook.HookFromAddress<FishingEventHandler.Delegates.PlayAnimation>((nint)FishingEventHandler.StaticVirtualTablePointer->PlayAnimation, PlayAnimationDetour);
        _updateCatchHook?.Enable();
        _useActionHook?.Enable();
        _playAnimationHook?.Enable();
        FshActions = ClassJob.Get(18).GetActions();

        Svc.GameInventory.InventoryChanged += OnInventoryChanged;
    }

    public void Dispose() {
        _useActionHook?.Dispose();
        _updateCatchHook?.Dispose();
        _playAnimationHook?.Dispose();
        Svc.GameInventory.InventoryChanged -= OnInventoryChanged;
    }

    /// <summary>
    /// Push current game state into WorldState. Call every frame
    /// </summary>
    public void Update() {
        if (Player.ClassJob.RowId is not 18 || Svc.Objects.LocalPlayer is null) return;
        var ws = Service.WorldState;
        ws.Execute(CollectGp());
        ws.Execute(CollectStatuses());
        ws.Execute(CollectOceanFishing());

        var biteContext = CollectBiteContext(ws);

        ws.Execute(CollectFishingState(biteContext));

        if (_needInventoryUpdate) {
            ws.Execute(CollectItemCounts());
            _needInventoryUpdate = false;
        }

        ws.Execute(CollectSwimbaitIds());
        ws.Execute(CollectPotCooldown());

        ws.Execute(CollectZone());

        ws.Execute(new FishingInfo.OpBiteContext(biteContext.BiteTimeSeconds, biteContext.ChumActive));
        ws.Execute(new FishingInfo.OpIntuition(new IntuitionInfo(biteContext.IntuitionStatus, biteContext.IntuitionTimeRemaining)));
    }

    /// <summary>
    /// Re-read fishing handler bait/swimbait/mooch ids from the game and apply to <see cref="Service.WorldState"/>.
    /// Call after commands like swimbait slot swap so hook/timeout logic sees the new selection immediately.
    /// </summary>
    public void RefreshFishingStateSnapshot() {
        if (Player.ClassJob.RowId is not 18 || Svc.Objects.LocalPlayer is null)
            return;

        var ws = Service.WorldState;
        var biteContext = CollectBiteContext(ws);
        ws.Execute(CollectFishingState(biteContext));
        ws.Execute(CollectSwimbaitIds());
        ws.Execute(new FishingInfo.OpBiteContext(biteContext.BiteTimeSeconds, biteContext.ChumActive));
        ws.Execute(new FishingInfo.OpIntuition(new IntuitionInfo(biteContext.IntuitionStatus, biteContext.IntuitionTimeRemaining)));
    }

    private void OnInventoryChanged(IReadOnlyCollection<InventoryEventArgs> _)
        => _needInventoryUpdate = true;

    private static BiteContext CollectBiteContext(WorldState ws) {
        return new BiteContext {
            BiteTimeSeconds = ws.Fishing.BiteInfo.BiteTimeSeconds,
            ChumActive = ws.Player.HasStatus(IDs.Status.Chum),
            IntuitionStatus = ws.Player.HasStatus(IDs.Status.FishersIntuition) ? IntuitionStatus.Active : IntuitionStatus.NotActive,
            IntuitionTimeRemaining = ws.Player.GetStatusTime(IDs.Status.FishersIntuition),
            SpectralCurrentStatus = ws.Ocean.SpectralCurrentStatus,
            LastCaughtFishId = ws.Fishing.LastCatch?.FishId,
        };
    }

    private static unsafe OceanFishInfo.OpOceanFishing CollectOceanFishing() {
        try {
            var ptr = EventFramework.Instance()->GetInstanceContentOceanFishing();
            if (ptr == null)
                return new OceanFishInfo.OpOceanFishing(null);

            var fishData = new List<InstanceContentOceanFishing.FishDataStruct>(60);
            foreach (var f in ptr->FirstZoneFishData)
                fishData.Add(f);
            foreach (var f in ptr->SecondZoneFishData)
                fishData.Add(f);
            foreach (var f in ptr->ThirdZoneFishData)
                fishData.Add(f);

            var state = new OceanFishingState {
                SpectralCurrentActive = ptr->SpectralCurrentActive,
                CurrentRoute = ptr->CurrentRoute,
                CurrentZone = ptr->CurrentZone,
                Mission1 = new OceanMission(ptr->Mission1Type, ptr->Mission1Progress),
                Mission2 = new OceanMission(ptr->Mission2Type, ptr->Mission2Progress),
                Mission3 = new OceanMission(ptr->Mission3Type, ptr->Mission3Progress),
                FishData = fishData,
            };
            return new OceanFishInfo.OpOceanFishing(state);
        }
        catch {
            return new OceanFishInfo.OpOceanFishing(null);
        }
    }

    private unsafe bool UseActionDetour(ActionManager* thisPtr, ActionType actionType, uint actionId, ulong targetId, uint extraParam, ActionManager.UseActionMode mode, uint comboRouteId, bool* outOptAreaTargeted) {
        try {
            if (actionType == ActionType.Action && Service.Configuration.PluginEnabled && Service.WorldState.ActionAvailable(actionId, actionType))
                Service.WorldState.Execute(new FishingInfo.OpPlayerUsedAction(new UsedAction(actionId, actionType)));
        }
        catch (Exception e) {
            Service.PrintDebug($"[WorldStateUpdater] UseAction: {e.Message}");
        }
        return _useActionHook!.Original(thisPtr, actionType, actionId, targetId, extraParam, mode, comboRouteId, outOptAreaTargeted);
    }

    private unsafe void UpdateCatchDetour(AgentCatch* thisPtr, uint itemId, bool isLarge, ushort size, byte amount, byte level, byte stars, byte oceanStars, bool isMoochable, bool isFirstTimeCatch, byte a11, byte a12) {
        _updateCatchHook!.Original(thisPtr, itemId, isLarge, size, amount, level, stars, oceanStars, isMoochable, isFirstTimeCatch, a11, a12);
        var fishId = (int?)ItemUtil.GetBaseId(itemId).ItemId;
        if (fishId is { } id && id > 0)
            Service.WorldState.Execute(new FishingInfo.OpSetLastCatch(new CatchInfo(id, amount, isLarge, size, level, stars, oceanStars, isMoochable, isFirstTimeCatch)));
        Service.WorldState.Execute(new FishingInfo.OpSetFishingStep(FishingSteps.FishCaught));
    }

    private unsafe bool PlayAnimationDetour(FishingEventHandler* thisPtr, Character* chara, ushort actionTimelineId, ulong a4) {
        var tugType = (FishingHookStrength)actionTimelineId;
        if (tugType is FishingHookStrength.Weak or FishingHookStrength.Strong or FishingHookStrength.Legendary) {
            Service.WorldState.Execute(new FishingInfo.OpSetFishingStep(FishingSteps.FishBit));
            Service.WorldState.Execute(new FishingInfo.OpTugType(tugType));
        }
        else
            Service.WorldState.Execute(new FishingInfo.OpTugType(0));
        return _playAnimationHook!.Original(thisPtr, chara, actionTimelineId, a4);
    }

    public static int ComputeCurrentBaitMoochId(uint currentId, uint? swimbaitId, bool isMooching, BiteContext biteContext) {
        if (swimbaitId.HasValue && swimbaitId.Value != 0)
            return (int)swimbaitId.Value;
        if (GameRes.Fishes.Any(f => f.Id == currentId))
            return (int)currentId;
        if (isMooching && biteContext.LastCaughtFishId is { } lastId && lastId > 0 &&
            GameRes.Fishes.Any(f => f.Id == lastId))
            return lastId;
        return (int)currentId;
    }

    private static PlayerInfo.OpGp CollectGp()
        => new(Svc.Objects.LocalPlayer?.CurrentGp ?? 0, Svc.Objects.LocalPlayer?.MaxGp ?? 0);

    private static PlayerInfo.OpStatuses CollectStatuses() {
        var dict = new Dictionary<uint, (float Time, int Stacks)>();
        if (Svc.Objects.LocalPlayer is { StatusList: var statuses })
            foreach (var buff in statuses)
                dict[buff.StatusId] = (buff.RemainingTime, buff.Param);
        return new PlayerInfo.OpStatuses(dict);
    }

    private static unsafe FishingInfo.OpFishingState CollectFishingState(BiteContext biteContext) {
        var state = FishingState.None;
        uint baitId = 0;
        uint? swimbaitId = null;
        var isMooching = false;
        var baitMoochId = 0;

        try {
            if (Player.Territory is { Value.TerritoryIntendedUse.RowId: 60 }) {
                if (WKSManager.Instance() is not null and var cosmic)
                    baitId = cosmic->FishingBait;
            }
            else
                baitId = FFXIVClientStructs.FFXIV.Client.Game.UI.PlayerState.Instance()->FishingBait;

            var ef = EventFramework.Instance();
            var handler = ef != null ? ef->EventHandlerModule.FishingEventHandler : null;
            if (handler != null) {
                state = handler->State;
                if (handler->CurrentSelectedSwimBait is >= 0 and < 3)
                    swimbaitId = handler->SwimBaitItemIds[handler->CurrentSelectedSwimBait];
                var flags = handler->CurrentCastBaitFlags;
                isMooching = (flags & (FishingBaitFlags.Mooch | FishingBaitFlags.Swimbait)) != 0;

                Service.WorldState.Execute(new FishingInfo.OpFishingHandlerState(
                    new PreviousCatchInfo(
                        handler->CanMoochPreviousCatch,
                        handler->CanMooch2PreviousCatch,
                        handler->CanReleasePreviousCatch,
                        handler->CanIdenticalCastPreviousCatch,
                        handler->CanSurfaceSlapPreviousCatch),
                    handler->CanFish,
                    handler->ChangingPosition,
                    handler->CurrentCastBaitFlags,
                    handler->CurrentSelectedSwimBait,
                    handler->MoochOpportunityExpirationTime,
                    handler->CatchActionExpirationTime));
            }

            baitMoochId = ComputeCurrentBaitMoochId(baitId, swimbaitId, isMooching, biteContext);
        }
        catch { }

        return new FishingInfo.OpFishingState(state, new BaitInfo(baitId, swimbaitId, baitMoochId, isMooching));
    }

    private static unsafe PlayerInfo.OpItemCounts CollectItemCounts() {
        var dict = new Dictionary<uint, int>();
        try {
            var inv = InventoryManager.Instance();
            if (inv != null) {
                for (var i = 0; i < 4; i++) {
                    var container = inv->GetInventoryContainer((InventoryType)i);
                    if (container == null) continue;
                    for (var k = 0; k < container->Size; k++) {
                        var slot = container->GetInventorySlot(k);
                        if (slot == null || slot->ItemId == 0) continue;
                        var id = slot->ItemId;
                        dict[id] = dict.GetValueOrDefault(id, 0) + slot->Quantity;
                    }
                }
            }
        }
        catch { }
        return new PlayerInfo.OpItemCounts(dict);
    }

    private static unsafe FishingInfo.OpSwimbaitIds CollectSwimbaitIds() {
        var list = new List<uint>();
        try {
            if (EventFramework.Instance() is not null and var ef && ef->EventHandlerModule.FishingEventHandler is not null and var handler)
                list.Add(handler->SwimBaitItemIds.ToArray());
        }
        catch { }
        return new FishingInfo.OpSwimbaitIds(list);
    }

    private static unsafe PlayerInfo.OpPotCooldown CollectPotCooldown() {
        var off = false;
        try {
            var recast = ActionManager.Instance()->GetRecastGroupDetail(68);
            off = recast->Total - recast->Elapsed <= 0;
        }
        catch { }
        return new PlayerInfo.OpPotCooldown(off);
    }

    private static unsafe WorldState.OpZone CollectZone()
        => new(WeatherManager.Instance()->GetCurrentWeather(), Svc.ClientState.TerritoryType);
}
