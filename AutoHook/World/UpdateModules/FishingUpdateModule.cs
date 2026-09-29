using FFXIVClientStructs.FFXIV.Client.Game.Event;
using FFXIVClientStructs.FFXIV.Client.Game.WKS;

namespace AutoHook.World.UpdateModules;

public sealed class FishingUpdateModule : IWorldUpdateModule {
    private const float BiteTimeLogThreshold = 0.25f;
    private static readonly HashSet<uint> FishIdSet = [];

    private readonly List<uint> _swimbaitScratch = [];

    public BiteContext LastBiteContext { get; private set; }

    public void Update(WorldState ws) => Run(ws);

    public void Refresh(WorldState ws) => Run(ws);

    private void Run(WorldState ws) {
        var biteContext = CollectBiteContext(ws);
        UpdateFishingState(ws, biteContext);
        UpdateSwimbaitIds(ws);
        UpdateBiteContext(ws, biteContext);
        UpdateIntuition(ws, biteContext);
        LastBiteContext = biteContext;
    }

    private static void UpdateBiteContext(WorldState ws, BiteContext biteContext) {
        var chumChanged = ws.Fishing.ChumActive != biteContext.ChumActive;
        var timeDelta = Math.Abs(ws.Fishing.BiteInfo.BiteTimeSeconds - biteContext.BiteTimeSeconds);
        if (!chumChanged && timeDelta < BiteTimeLogThreshold)
            return;
        ws.Execute(new RodState.OpBiteContext(biteContext.BiteTimeSeconds, biteContext.ChumActive));
    }

    private static void UpdateIntuition(WorldState ws, BiteContext biteContext) {
        // on edge we want to report intuition as "gained/lost", then wait for extraoptions to poll it and set it as "active/inactive"
        var isActive = biteContext.IntuitionStatus == IntuitionStatus.Active;
        var prev = ws.Fishing.Intuition.Status;
        var wasActive = prev is IntuitionStatus.Active or IntuitionStatus.Gained;

        var nextStatus = isActive ? wasActive ? prev : IntuitionStatus.Gained : wasActive ? IntuitionStatus.Lost : prev;
        var next = new IntuitionInfo(nextStatus, isActive ? biteContext.IntuitionTimeRemaining : 0f);
        if (ws.Fishing.Intuition == next)
            return;
        ws.Execute(new RodState.OpIntuition(next));
    }

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

    private static unsafe void UpdateFishingState(WorldState ws, BiteContext biteContext) {
        var state = FishingState.None;
        uint baitId = 0;
        uint? swimbaitId = null;
        var isMooching = false;
        PreviousCatchInfo previousCatch = default;
        var canFish = false;
        var changingPosition = false;
        FishingBaitFlags castFlags = 0;
        sbyte selectedSwimbait = 0;
        long moochExpire = 0;
        long catchExpire = 0;

        try {
            if (Player.Territory is { Value.TerritoryIntendedUse.RowId: 60 }) {
                if (WKSManager.Instance() is not null and var cosmic)
                    baitId = cosmic->State.FishingBait;
            }
            else
                baitId = GamePlayerState.Instance()->FishingBait;

            var ef = EventFramework.Instance();
            var handler = ef != null ? ef->EventHandlerModule.FishingEventHandler : null;
            if (handler != null) {
                state = handler->State;
                if (handler->CurrentSelectedSwimBait is >= 0 and < 3)
                    swimbaitId = handler->SwimBaitItemIds[handler->CurrentSelectedSwimBait];
                var flags = handler->CurrentCastBaitFlags;
                isMooching = (flags & (FishingBaitFlags.Mooch | FishingBaitFlags.Swimbait)) != 0;
                previousCatch = new PreviousCatchInfo(handler->CanMoochPreviousCatch, handler->CanMooch2PreviousCatch, handler->CanReleasePreviousCatch, handler->CanIdenticalCastPreviousCatch, handler->CanSurfaceSlapPreviousCatch);
                canFish = handler->CanFish;
                changingPosition = handler->ChangingPosition;
                castFlags = handler->CurrentCastBaitFlags;
                selectedSwimbait = handler->CurrentSelectedSwimBait;
                moochExpire = handler->MoochOpportunityExpirationTime;
                catchExpire = handler->CatchActionExpirationTime;
            }
        }
        catch { }

        var baitMoochId = ComputeCurrentBaitMoochId(baitId, swimbaitId, isMooching, biteContext);
        var bait = new BaitInfo(baitId, swimbaitId, baitMoochId, isMooching);

        if (ws.Fishing.FishingState != state || ws.Fishing.BaitInfo != bait)
            ws.Execute(new RodState.OpFishingState(state, bait));

        var handlerState = new RodState.OpFishingHandlerState(previousCatch, canFish, changingPosition, castFlags, selectedSwimbait, moochExpire, catchExpire);
        var f = ws.Fishing;
        if (f.PreviousCatch != previousCatch || f.CanFish != canFish || f.ChangingPosition != changingPosition ||
            f.CurrentCastBaitFlags != castFlags || f.CurrentSelectedSwimbait != selectedSwimbait ||
            f.MoochOpportunityExpirationTime != moochExpire || f.CatchActionExpirationTime != catchExpire)
            ws.Execute(handlerState);
    }

    private unsafe void UpdateSwimbaitIds(WorldState ws) {
        _swimbaitScratch.Clear();
        try {
            if (EventFramework.Instance() is not null and var ef && ef->EventHandlerModule.FishingEventHandler is not null and var handler)
                _swimbaitScratch.Add(handler->SwimBaitItemIds.ToArray());
        }
        catch { }

        if (SwimbaitIdsEqual(ws.Fishing.SwimbaitIds, _swimbaitScratch))
            return;

        ws.Execute(new RodState.OpSwimbaitIds([.. _swimbaitScratch]));
    }

    private static bool SwimbaitIdsEqual(List<uint> current, List<uint> next) {
        if (current.Count != next.Count)
            return false;
        for (var i = 0; i < current.Count; i++) {
            if (current[i] != next[i])
                return false;
        }
        return true;
    }

    public static uint ComputeCurrentBaitMoochId(uint currentId, uint? swimbaitId, bool isMooching, BiteContext biteContext) {
        if (swimbaitId.HasValue && swimbaitId.Value != 0)
            return swimbaitId.Value;
        if (FishIdSet.Count == 0) {
            foreach (var fish in FishBaitCatalog.Get().Fishes)
                FishIdSet.Add((uint)fish.Id);
        }
        if (FishIdSet.Contains(currentId))
            return currentId;
        if (isMooching && biteContext.LastCaughtFishId is { } lastId && lastId > 0 && FishIdSet.Contains(lastId))
            return lastId;
        return currentId;
    }
}
