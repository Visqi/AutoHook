using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Event;
using FFXIVClientStructs.FFXIV.Client.Game.InstanceContent;

namespace AutoHook.Conditions;

/// <summary>Ocean fishing instance state from <see cref="InstanceContentOceanFishing"/>.</summary>
public sealed class OceanFishingState
{
    public static readonly OceanFishingState Empty = new();

    public bool SpectralCurrentActive { get; init; }
    public uint CurrentRoute { get; init; }
    public uint CurrentZone { get; init; }
    public uint Mission1Type { get; init; }
    public uint Mission2Type { get; init; }
    public uint Mission3Type { get; init; }
    public ushort Mission1Progress { get; init; }
    public ushort Mission2Progress { get; init; }
    public ushort Mission3Progress { get; init; }
    public IReadOnlyList<InstanceContentOceanFishing.FishDataStruct> FishData { get; init; } = [];

    public OceanFishingState WithSpectralCurrentActive(bool value) => new()
    {
        SpectralCurrentActive = value,
        CurrentRoute = CurrentRoute,
        CurrentZone = CurrentZone,
        Mission1Type = Mission1Type,
        Mission2Type = Mission2Type,
        Mission3Type = Mission3Type,
        Mission1Progress = Mission1Progress,
        Mission2Progress = Mission2Progress,
        Mission3Progress = Mission3Progress,
        FishData = FishData,
    };
}

public sealed class WorldState
{
    public uint CurrentGp { get; private set; }
    public uint MaxGp { get; private set; }

    private readonly Dictionary<uint, (float Time, int Stacks)> _statuses = [];
    public IReadOnlyDictionary<uint, (float Time, int Stacks)> Statuses => _statuses;

    public FishingState FishingState { get; private set; }
    public FishingState PreviousFishingState { get; private set; }
    public uint CurrentBaitId { get; private set; }
    public uint? CurrentSwimbaitId { get; private set; }
    public bool IsMooching { get; private set; }
    public int CurrentBaitMoochId { get; private set; }

    public double BiteTimeSeconds { get; private set; }
    public bool ChumActive { get; private set; }

    public IntuitionStatus IntuitionStatus { get; private set; }
    public SpectralCurrentStatus SpectralCurrentStatus { get; private set; }
    public float IntuitionTimeRemaining { get; private set; }

    public OceanFishingState OceanFishing { get; private set; } = OceanFishingState.Empty;

    private readonly Dictionary<(uint Id, ActionType Type), bool> _actionAvailable = [];
    public bool ActionAvailable(uint actionId, ActionType actionType = ActionType.Action)
        => _actionAvailable.TryGetValue((actionId, actionType), out var v) && v;

    /// <summary>When true, cast is blocked (e.g. delay after using an action)</summary>
    public bool BlockCasting { get; private set; }

    private readonly Dictionary<uint, int> _itemCounts = [];
    public int GetItemCount(uint itemId) => _itemCounts.TryGetValue(itemId, out var c) ? c : 0;
    public bool HasItem(uint itemId) => GetItemCount(itemId) > 0;
    public bool HaveCordialInInventory(uint id) => HasItem(id);

    // Per-fish catch counters keyed by fish ID for the current session.
    private readonly Dictionary<int, int> _fishCaughtCounts = [];
    public int GetFishCaughtCount(int fishId) => _fishCaughtCounts.TryGetValue(fishId, out var c) ? c : 0;

    private readonly List<uint> _swimbaitIds = [];
    public IReadOnlyList<uint> SwimbaitIds => _swimbaitIds;
    public int GetSwimbaitCount() => _swimbaitIds.Count(id => id != 0);
    public int GetSwimbaitCountForFish(uint fishId) => _swimbaitIds.Count(id => id == fishId);
    public bool IsSwimbaitFull() => GetSwimbaitCount() >= 3;
    public bool IsSwimbaitEmpty() => GetSwimbaitCount() == 0;

    public bool IsPotOffCooldown { get; private set; }

    public byte CurrentWeatherId { get; private set; }
    public uint TerritoryId { get; private set; }

    public int? LastCaughtFishId { get; private set; }
    public byte LastCatchAmount { get; private set; }

    public FishingSteps FishingStep { get; private set; }
    public uint LastUsedActionId { get; private set; }
    public ActionType LastUsedActionType { get; private set; }

    /// <summary>Set when a lure (Ambitious/Modest) successfully applied. Cleared on new cast.</summary>
    public bool LureSuccess { get; private set; }

    /// <summary>True when this session began with Mooch (set in OnBeganFishing).</summary>
    public bool SessionIsMooching { get; private set; }

    public bool HasStatus(uint statusId) => _statuses.ContainsKey(statusId);
    public float GetStatusTime(uint statusId) => _statuses.TryGetValue(statusId, out var t) ? t.Time : 0f;
    public int GetStatusStacks(uint statusId) => _statuses.TryGetValue(statusId, out var s) ? s.Stacks : 0;

    public bool HasAnyStatus(uint[] statusIds)
    {
        foreach (var id in statusIds)
            if (HasStatus(id)) return true;
        return false;
    }

    public bool HasAnglersArtStacks(int amount) => GetStatusStacks(IDs.Status.AnglersArt) >= amount;

    public bool IsMoochAvailable()
        => ActionAvailable(IDs.Actions.Mooch) || ActionAvailable(IDs.Actions.Mooch2);

    public bool IsCastAvailable()
        => ActionAvailable(IDs.Actions.Cast) && !BlockCasting;

    public bool HasMultihookAvailable()
        => ActionAvailable(IDs.Actions.MultiHook, ActionType.EventAction);

    /// <summary>Current bait or swimbait ID (swimbait takes precedence).</summary>
    public uint CurrentBaitSwimBait => CurrentSwimbaitId ?? CurrentBaitId;

    public event Action<Operation>? Modified;

    public abstract record Operation
    {
        internal void Execute(WorldState ws)
        {
            Exec(ws);
            ws.Modified?.Invoke(this);
        }

        protected abstract void Exec(WorldState ws);
    }

    public void Execute(Operation op)
    {
        op.Execute(this);
    }

    public sealed record OpGp(uint Current, uint Max) : Operation
    {
        protected override void Exec(WorldState ws)
        {
            ws.CurrentGp = Current;
            ws.MaxGp = Max;
        }
    }

    public sealed record OpStatuses(IReadOnlyDictionary<uint, (float Time, int Stacks)> Statuses) : Operation
    {
        protected override void Exec(WorldState ws)
        {
            ws._statuses.Clear();
            foreach (var kv in Statuses)
                ws._statuses[kv.Key] = kv.Value;
        }
    }

    public sealed record OpFishingState(FishingState State, uint BaitId, uint? SwimbaitId, bool IsMooching, int BaitMoochId = 0) : Operation
    {
        protected override void Exec(WorldState ws)
        {
            ws.FishingState = State;
            ws.CurrentBaitId = BaitId;
            ws.CurrentSwimbaitId = SwimbaitId;
            ws.IsMooching = IsMooching;
            ws.CurrentBaitMoochId = BaitMoochId;
        }
    }

    public sealed record OpSetBlockCasting(bool Block) : Operation
    {
        protected override void Exec(WorldState ws) => ws.BlockCasting = Block;
    }

    public sealed record OpItemCounts(IReadOnlyDictionary<uint, int> Counts) : Operation
    {
        protected override void Exec(WorldState ws)
        {
            ws._itemCounts.Clear();
            foreach (var kv in Counts)
                ws._itemCounts[kv.Key] = kv.Value;
        }
    }

    public sealed record OpSwimbaitIds(IReadOnlyList<uint> Ids) : Operation
    {
        protected override void Exec(WorldState ws)
        {
            ws._swimbaitIds.Clear();
            ws._swimbaitIds.AddRange(Ids);
        }
    }

    public sealed record OpPotCooldown(bool OffCooldown) : Operation
    {
        protected override void Exec(WorldState ws) => ws.IsPotOffCooldown = OffCooldown;
    }

    public sealed record OpBiteContext(double BiteTimeSeconds, bool ChumActive) : Operation
    {
        protected override void Exec(WorldState ws)
        {
            ws.BiteTimeSeconds = BiteTimeSeconds;
            ws.ChumActive = ChumActive;
        }
    }

    public sealed record OpIntuition(IntuitionStatus Status, float TimeRemaining) : Operation
    {
        protected override void Exec(WorldState ws)
        {
            ws.IntuitionStatus = Status;
            ws.IntuitionTimeRemaining = TimeRemaining;
        }
    }

    public sealed record OpOceanFishing(OceanFishingState? State) : Operation
    {
        protected override void Exec(WorldState ws)
        {
            var s = State ?? OceanFishingState.Empty;
            ws.OceanFishing = s;
            ws.SpectralCurrentStatus = s.SpectralCurrentActive ? SpectralCurrentStatus.Active : SpectralCurrentStatus.NotActive;
        }
    }

    public sealed record OpLastCatch(int? FishId, byte Amount = 1) : Operation
    {
        protected override void Exec(WorldState ws)
        {
            ws.LastCaughtFishId = FishId;
            ws.LastCatchAmount = Amount;
        }
    }

    public sealed record OpSetFishingStep(FishingSteps Step) : Operation
    {
        protected override void Exec(WorldState ws) => ws.FishingStep = Step;
    }

    public sealed record OpOrFishingStep(FishingSteps Flag) : Operation
    {
        protected override void Exec(WorldState ws) => ws.FishingStep |= Flag;
    }

    public sealed record OpPlayerUsedAction(ActionType ActionType, uint ActionId) : Operation
    {
        protected override void Exec(WorldState ws)
        {
            ws.LastUsedActionType = ActionType;
            ws.LastUsedActionId = ActionId;
        }
    }

    public sealed record OpSetLureSuccess(bool Value) : Operation
    {
        protected override void Exec(WorldState ws) => ws.LureSuccess = Value;
    }

    public sealed record OpSetSessionIsMooching(bool Value) : Operation
    {
        protected override void Exec(WorldState ws) => ws.SessionIsMooching = Value;
    }

    public sealed record OpSetPreviousFishingState(FishingState Value) : Operation
    {
        protected override void Exec(WorldState ws) => ws.PreviousFishingState = Value;
    }

    public sealed record OpActionAvailability(IReadOnlyCollection<(uint Id, ActionType Type)> Available) : Operation
    {
        protected override void Exec(WorldState ws)
        {
            ws._actionAvailable.Clear();
            foreach (var (id, type) in Available)
                ws._actionAvailable[(id, type)] = true;
        }
    }

    public sealed record OpZone(byte WeatherId, uint TerritoryId) : Operation
    {
        protected override void Exec(WorldState ws)
        {
            ws.CurrentWeatherId = WeatherId;
            ws.TerritoryId = TerritoryId;
        }
    }

    /// <summary>Increment per-fish caught counter for the current session.</summary>
    public sealed record OpAddFishCaught(int FishId, byte Amount) : Operation
    {
        protected override void Exec(WorldState ws)
        {
            if (FishId <= 0 || Amount <= 0)
                return;
            ws._fishCaughtCounts[FishId] = ws._fishCaughtCounts.GetValueOrDefault(FishId) + Amount;
        }
    }

    /// <summary>Reset all per-fish caught counters (typically on fishing stop).</summary>
    public sealed record OpResetFishCaught() : Operation
    {
        protected override void Exec(WorldState ws) => ws._fishCaughtCounts.Clear();
    }
}
