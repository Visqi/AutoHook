using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Event;

namespace AutoHook.Data;

public readonly record struct BiteInfo(double BiteTimeSeconds, FishingHookStrength TugType) {
    public override string ToString()
        => $"Time={BiteTimeSeconds:F2}, Tug={TugType}";
}

public readonly record struct BaitInfo(uint BaitId, uint? SelectedSwimbaitId, int MoochId, bool IsMooching) {
    public override string ToString()
        => $"BaitId={BaitId}, SwimbaitId={SelectedSwimbaitId}, MoochId={MoochId}, IsMooching={IsMooching}";
}

public readonly record struct IntuitionInfo(IntuitionStatus Status, float TimeRemaining) {
    public override string ToString()
        => $"Status={Status}, TimeRemaining={TimeRemaining:F1}s";
}

public readonly record struct UsedAction(uint ActionId, ActionType ActionType) {
    public override string ToString()
        => $"ActionId={ActionId}, Type={ActionType}";
}

public readonly record struct PreviousCatchInfo(
    bool CanMoochPreviousCatch,
    bool CanMooch2PreviousCatch,
    bool CanReleasePreviousCatch,
    bool CanIdenticalCastPreviousCatch,
    bool CanSurfaceSlapPreviousCatch) {
    public override string ToString()
        => $"Mooch={CanMoochPreviousCatch}, Mooch2={CanMooch2PreviousCatch}, Release={CanReleasePreviousCatch}, IC={CanIdenticalCastPreviousCatch}, SS={CanSurfaceSlapPreviousCatch}";
}

public readonly record struct CatchInfo(
    int FishId,
    byte Amount,
    bool IsLarge,
    ushort Size,
    byte Level,
    byte Stars,
    byte OceanStars,
    bool IsMoochable,
    bool IsFirstTimeCatch) {
    public override string ToString()
        => $"FishId={FishId}, Amount={Amount}, Large={IsLarge}, Size={Size}, Level={Level}, " +
           $"Stars={Stars}, OceanStars={OceanStars}, Moochable={IsMoochable}, FirstTime={IsFirstTimeCatch}";
}

public sealed class FishingInfo {
    public FishingState FishingState;
    public FishingState PreviousFishingState;
    public FishingSteps FishingStep;

    public BaitInfo BaitInfo;
    public BiteInfo BiteInfo;
    public bool ChumActive;

    public IntuitionInfo Intuition;
    public CatchInfo? LastCatch;
    public UsedAction? LastUsedAction;

    public bool LureSuccess;
    public PreviousCatchInfo PreviousCatch;

    public bool CanFish;
    public bool ChangingPosition;
    public FishingBaitFlags CurrentCastBaitFlags;
    public sbyte CurrentSelectedSwimbait;
    public long MoochOpportunityExpirationTime;
    public long CatchActionExpirationTime;

    public readonly List<CatchInfo> SessionCatches = [];

    public readonly Dictionary<int, int> FishCaughtCounts = [];
    public readonly List<uint> SwimbaitIds = [];

    public int GetFishCaughtCount(int fishId) => FishCaughtCounts.TryGetValue(fishId, out var c) ? c : 0;

    public IEnumerable<WorldState.Operation> CompareToInitial() {
        if (FishingState != FishingState.None || BaitInfo.BaitId != 0 || BaitInfo.SelectedSwimbaitId != null || BaitInfo.IsMooching || BaitInfo.MoochId != 0)
            yield return new OpFishingState(FishingState, BaitInfo);
        if (BiteInfo.BiteTimeSeconds != 0 || ChumActive)
            yield return new OpBiteContext(BiteInfo.BiteTimeSeconds, ChumActive);
        if (BiteInfo.TugType != 0)
            yield return new OpTugType(BiteInfo.TugType);
        if (Intuition.Status != IntuitionStatus.NotActive || Intuition.TimeRemaining != 0)
            yield return new OpIntuition(Intuition);
        if (LastCatch is { } lc && lc.FishId > 0 && lc.Amount > 0)
            yield return new OpSetLastCatch(lc);
        if (FishingStep != FishingSteps.None)
            yield return new OpSetFishingStep(FishingStep);
        if (PreviousFishingState != FishingState.None)
            yield return new OpSetPreviousFishingState(PreviousFishingState);
        if (LureSuccess)
            yield return new OpSetLureSuccess(LureSuccess);
        if (PreviousCatch.CanMoochPreviousCatch || PreviousCatch.CanMooch2PreviousCatch ||
            PreviousCatch.CanReleasePreviousCatch || PreviousCatch.CanIdenticalCastPreviousCatch || PreviousCatch.CanSurfaceSlapPreviousCatch ||
            CanFish || ChangingPosition || CurrentCastBaitFlags != 0 || CurrentSelectedSwimbait != 0 ||
            MoochOpportunityExpirationTime != 0 || CatchActionExpirationTime != 0)
            yield return new OpFishingHandlerState(PreviousCatch, CanFish, ChangingPosition, CurrentCastBaitFlags, CurrentSelectedSwimbait, MoochOpportunityExpirationTime, CatchActionExpirationTime);
        if (LastUsedAction is { } ua && ua.ActionId != 0)
            yield return new OpPlayerUsedAction(ua);
        foreach (var (fishId, count) in FishCaughtCounts) {
            if (fishId > 0 && count > 0 && count <= byte.MaxValue)
                yield return new OpAddFishCaught(fishId, (byte)count);
        }
        if (SwimbaitIds.Count != 0)
            yield return new OpSwimbaitIds(SwimbaitIds);
    }

    public sealed record OpFishingState(FishingState State, BaitInfo Bait) : WorldState.Operation {
        protected override void Exec(WorldState ws) {
            ws.Fishing.FishingState = State;
            ws.Fishing.BaitInfo = Bait;
        }
    }

    public sealed record OpBiteContext(double BiteTimeSeconds, bool ChumActive) : WorldState.Operation {
        protected override void Exec(WorldState ws) {
            var b = ws.Fishing.BiteInfo;
            ws.Fishing.BiteInfo = b with { BiteTimeSeconds = BiteTimeSeconds };
            ws.Fishing.ChumActive = ChumActive;
        }
    }

    public sealed record OpIntuition(IntuitionInfo Value) : WorldState.Operation {
        protected override void Exec(WorldState ws) => ws.Fishing.Intuition = Value;
    }

    public sealed record OpSetLastCatch(CatchInfo Value) : WorldState.Operation {
        protected override void Exec(WorldState ws) {
            ws.Fishing.LastCatch = Value;
            if (Value.FishId > 0 && Value.Amount > 0)
                ws.Fishing.SessionCatches.Add(Value);
        }
    }

    public sealed record OpSetFishingStep(FishingSteps Step, bool Or = false) : WorldState.Operation {
        protected override void Exec(WorldState ws)
            => ws.Fishing.FishingStep = Or ? ws.Fishing.FishingStep | Step : Step;
    }

    public sealed record OpClearFishingStepFlag(FishingSteps Flag) : WorldState.Operation {
        protected override void Exec(WorldState ws) => ws.Fishing.FishingStep &= ~Flag;
    }

    public sealed record OpPlayerUsedAction(UsedAction Value) : WorldState.Operation {
        protected override void Exec(WorldState ws) => ws.Fishing.LastUsedAction = Value;
    }

    public sealed record OpSetLureSuccess(bool Value) : WorldState.Operation {
        protected override void Exec(WorldState ws) => ws.Fishing.LureSuccess = Value;
    }

    public sealed record OpSetPreviousFishingState(FishingState Value) : WorldState.Operation {
        protected override void Exec(WorldState ws) => ws.Fishing.PreviousFishingState = Value;
    }

    public sealed record OpAddFishCaught(int FishId, byte Amount) : WorldState.Operation {
        protected override void Exec(WorldState ws) {
            if (FishId <= 0 || Amount <= 0)
                return;
            ws.Fishing.FishCaughtCounts[FishId] = ws.Fishing.FishCaughtCounts.GetValueOrDefault(FishId) + Amount;
        }
    }

    public sealed record OpResetFishCaught() : WorldState.Operation {
        protected override void Exec(WorldState ws) => ws.Fishing.FishCaughtCounts.Clear();
    }

    public sealed record OpClearSessionCatches() : WorldState.Operation {
        protected override void Exec(WorldState ws) {
            ws.Fishing.SessionCatches.Clear();
            ws.Fishing.LastCatch = null;
        }
    }

    public sealed record OpTugType(FishingHookStrength HookStrength) : WorldState.Operation {
        protected override void Exec(WorldState ws) {
            var b = ws.Fishing.BiteInfo;
            ws.Fishing.BiteInfo = b with { TugType = HookStrength };
        }
    }

    public sealed record OpFishingHandlerState(
        PreviousCatchInfo PreviousCatch,
        bool CanFish,
        bool ChangingPosition,
        FishingBaitFlags CurrentCastBaitFlags,
        sbyte CurrentSelectedSwimbait,
        long MoochOpportunityExpirationTime,
        long CatchActionExpirationTime) : WorldState.Operation {
        protected override void Exec(WorldState ws) {
            ws.Fishing.PreviousCatch = PreviousCatch;
            ws.Fishing.CanFish = CanFish;
            ws.Fishing.ChangingPosition = ChangingPosition;
            ws.Fishing.CurrentCastBaitFlags = CurrentCastBaitFlags;
            ws.Fishing.CurrentSelectedSwimbait = CurrentSelectedSwimbait;
            ws.Fishing.MoochOpportunityExpirationTime = MoochOpportunityExpirationTime;
            ws.Fishing.CatchActionExpirationTime = CatchActionExpirationTime;
        }
    }

    public sealed record OpSwimbaitIds(IReadOnlyList<uint> Ids) : WorldState.Operation {
        protected override void Exec(WorldState ws) {
            ws.Fishing.SwimbaitIds.Clear();
            ws.Fishing.SwimbaitIds.AddRange(Ids);
        }
    }
}
