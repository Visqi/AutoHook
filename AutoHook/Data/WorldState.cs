using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Event;
using Lumina.Excel.Sheets;

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
    public byte Level => Player.Level;
    public bool BlockCasting => Player.BlockCasting;

    public IReadOnlyDictionary<uint, (float Time, int Stacks)> Statuses => Player.Statuses;
    public bool HasStatus(uint statusId) => Player.HasStatus(statusId);
    public float GetStatusTime(uint statusId) => Player.GetStatusTime(statusId);
    public int GetStatusStacks(uint statusId) => Player.GetStatusStacks(statusId);
    public bool HasAnyStatus(uint[] statusIds) => Player.HasAnyStatus(statusIds);

    public bool HasAnglersArtStacks(int amount) => GetStatusStacks(IDs.Status.AnglersArt) >= amount;

    public bool BlocksFortune()
        => HasStatus(IDs.Status.MakeshiftBait)
           || HasStatus(IDs.Status.PrizeCatch)
           || HasStatus(IDs.Status.AnglersFortune);

    public unsafe bool ActionAvailable(uint actionId, ActionType actionType = ActionType.Action) {
        if (ActionManager.Instance()->GetActionStatus(actionType, actionId) != 0)
            return false;
        var group = ActionManager.Instance()->GetRecastGroup((int)actionType, actionId);
        if (group == -1)
            return true;
        var detail = ActionManager.Instance()->GetRecastGroupDetail(group);
        return detail->Total - detail->Elapsed <= 0;
    }

    public unsafe int GetItemCount(uint itemId) {
        if (WKSItemInfo.Any(r => r.Item.RowId == itemId && r.WKSItemSubCategory.RowId is 5)) {
            return ContentInventoryManager.Instance()->WKSInventoryProvider.Cosmopouch1.WKSItems.FirstOrNull(i => i.WKSItemId == itemId)?.WKSItemQuantity ?? 0;
        }
        return Player.GetItemCount(itemId);
    }

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
    public int GetFishCaughtCount(uint fishId) => Fishing.GetFishCaughtCount(fishId);

    public bool IsMoochAvailable()
        => ActionAvailable(IDs.Actions.Mooch) || ActionAvailable(IDs.Actions.Mooch2);

    public bool IsCastAvailable()
        => ActionAvailable(IDs.Actions.Cast) && !BlockCasting;

    public bool HasMultihookAvailable()
        => ActionAvailable(IDs.Actions.MultiHook, ActionType.Action);

    public bool IsStellarHooksetAvailable()
        => GetAvailableStellarHooksetId() is not null;

    // why tf did they have to name both the same thing
    public unsafe uint? GetAvailableStellarHooksetId() {
        if (ActionAvailable(IDs.Actions.StellarHookMaster)) {
            if (DutyActionManager.GetInstanceIfReady() is not null and var dm) {
                for (var i = 0; i < dm->NumValidSlots; i++)
                    if (dm->ActionId[i] is IDs.Actions.StellarHookMaster && dm->CurCharges[i] > 0)
                        return IDs.Actions.StellarHookMaster;
            }
            else
                return IDs.Actions.StellarHookMaster;
        }

        if (ActionAvailable(IDs.Actions.StellarHook))
            return IDs.Actions.StellarHook;

        return null;
    }

    public OceanFishingState OceanFishing => Ocean.OceanFishing;
    public SpectralCurrentStatus SpectralCurrentStatus => Ocean.SpectralCurrentStatus;
    public OceanSpectralTimerInfo SpectralTimer => Ocean.SpectralTimer;
    public float SpectralTimeRemaining => Ocean.SpectralTimer.TimeRemaining;
    public IReadOnlyList<ZoneSpectralRecord> SpectralHistory => Ocean.SpectralHistory;

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

    public sealed record OpSetFishingStep(FishingSteps Step, bool Or = false) : Operation {
        protected override void Exec(WorldState ws)
            => ws.Fishing.FishingStep = Or ? ws.Fishing.FishingStep | Step : Step;
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

    public Event<OpOceanZoneStarted> OceanZoneStarted = new();
    public sealed record OpOceanZoneStarted(uint ZoneIndex) : Operation {
        protected override void Exec(WorldState ws) => ws.OceanZoneStarted.Fire(this);
    }
}
