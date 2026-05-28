using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Event;

namespace AutoHook.Data;

public sealed class WorldState {
    public ulong QPF;
    public string GameVersion = string.Empty;
    public FrameState Frame;

    public readonly PlayerInfo Player = new();
    public readonly FishingInfo Fishing = new();
    public readonly OceanFishInfo Ocean = new();
    public readonly WKSInfo WKS = new();

    public byte CurrentWeatherId;
    public uint TerritoryId;

    public DateTime CurrentTime => Frame.Timestamp;
    public DateTime FutureTime(float deltaSeconds) => Frame.Timestamp.AddSeconds(deltaSeconds);

    public uint CurrentGp => Player.CurrentGp;
    public uint MaxGp => Player.MaxGp;
    public bool BlockCasting => Player.BlockCasting;

    public IReadOnlyDictionary<uint, (float Time, int Stacks)> Statuses => Player.Statuses;
    public bool HasStatus(uint statusId) => Player.HasStatus(statusId);
    public float GetStatusTime(uint statusId) => Player.GetStatusTime(statusId);
    public int GetStatusStacks(uint statusId) => Player.GetStatusStacks(statusId);
    public bool HasAnyStatus(uint[] statusIds) => Player.HasAnyStatus(statusIds);

    public bool HasAnglersArtStacks(int amount) => GetStatusStacks(IDs.Status.AnglersArt) >= amount;

    public unsafe bool ActionAvailable(uint actionId, ActionType actionType = ActionType.Action) {
        if (ActionManager.Instance()->GetActionStatus(actionType, actionId) != 0)
            return false;
        var group = ActionManager.Instance()->GetRecastGroup((int)actionType, actionId);
        if (group == -1)
            return true;
        var detail = ActionManager.Instance()->GetRecastGroupDetail(group);
        return detail->Total - detail->Elapsed <= 0;
    }

    public int GetItemCount(uint itemId) => Player.GetItemCount(itemId);
    public bool HasItem(uint itemId) => Player.HasItem(itemId);
    public bool HaveCordialInInventory(uint id) => Player.HaveCordialInInventory(id);

    public IReadOnlyList<uint> SwimbaitIds => Fishing.SwimbaitIds;

    /// <summary>Fish id used while evaluating swimbait slot conditions (0 = unset).</summary>
    public uint SwimbaitEvaluationFishId { get; set; }

    public int GetSwimbaitCount() => Fishing.SwimbaitIds.Count(id => id != 0);
    public int GetSwimbaitCountForFish(uint fishId) => Fishing.SwimbaitIds.Count(id => id == fishId);
    public bool IsSwimbaitFull() => GetSwimbaitCount() >= 3;
    public bool IsSwimbaitEmpty() => GetSwimbaitCount() == 0;

    public FishingState FishingState => Fishing.FishingState;
    public FishingState PreviousFishingState => Fishing.PreviousFishingState;
    public FishingSteps FishingStep => Fishing.FishingStep;

    public bool ChumActive => Fishing.ChumActive;
    public bool LureSuccess => Fishing.LureSuccess;
    public int GetFishCaughtCount(int fishId) => Fishing.GetFishCaughtCount(fishId);

    public bool IsMoochAvailable()
        => ActionAvailable(IDs.Actions.Mooch) || ActionAvailable(IDs.Actions.Mooch2);

    public bool IsCastAvailable()
        => ActionAvailable(IDs.Actions.Cast) && !BlockCasting;

    public bool HasMultihookAvailable()
        => ActionAvailable(IDs.Actions.MultiHook, ActionType.EventAction);

    public OceanFishingState OceanFishing => Ocean.OceanFishing;
    public SpectralCurrentStatus SpectralCurrentStatus => Ocean.SpectralCurrentStatus;

    public event Action<Operation>? Modified;

    public abstract record Operation {
        internal void Execute(WorldState ws) {
            Exec(ws);
            ws.Modified?.Invoke(this);
        }

        protected abstract void Exec(WorldState ws);
    }

    public void Execute(Operation op) {
        op.Execute(this);
    }

    public IEnumerable<Operation> CompareToInitial() {
        if (CurrentTime != default)
            yield return new OpFrameStart(Frame);
        if (CurrentWeatherId != 0 || TerritoryId != 0)
            yield return new OpZone(CurrentWeatherId, TerritoryId);
        foreach (var o in Player.CompareToInitial())
            yield return o;
        foreach (var o in Fishing.CompareToInitial())
            yield return o;
        foreach (var o in Ocean.CompareToInitial())
            yield return o;
        foreach (var o in WKS.CompareToInitial())
            yield return o;
    }

    public sealed record OpFrameStart(FrameState Frame) : Operation {
        protected override void Exec(WorldState ws) {
            ws.Frame = Frame;
        }
    }

    public sealed record OpZone(byte WeatherId, uint TerritoryId) : Operation {
        protected override void Exec(WorldState ws) {
            ws.CurrentWeatherId = WeatherId;
            ws.TerritoryId = TerritoryId;
        }
    }

    public sealed record OpSetBlockCasting(bool Block) : Operation {
        protected override void Exec(WorldState ws) => ws.Player.BlockCasting = Block;
    }

    public sealed record OpSetFishingStep(FishingSteps Step) : Operation {
        protected override void Exec(WorldState ws) => ws.Fishing.FishingStep = Step;
    }

    public sealed record OpOrFishingStep(FishingSteps Flag) : Operation {
        protected override void Exec(WorldState ws) => ws.Fishing.FishingStep |= Flag;
    }

    public sealed record OpClearFishingStepFlag(FishingSteps Flag) : Operation {
        protected override void Exec(WorldState ws) => ws.Fishing.FishingStep &= ~Flag;
    }

    public Event<OpBeganSession> BeganSession = new();
    public sealed record OpBeganSession() : Operation {
        protected override void Exec(WorldState ws) {
            ws.BeganSession.Fire(this);
        }
    }

    public Event<OpEndedSession> EndedSession = new();
    public sealed record OpEndedSession() : Operation {
        protected override void Exec(WorldState ws) {
            ws.EndedSession.Fire(this);
        }
    }
}
