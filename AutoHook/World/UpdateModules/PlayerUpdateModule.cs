using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.Interop;
using Lumina.Excel.Sheets;
using System.Reflection;

namespace AutoHook.World.UpdateModules;

public sealed class PlayerUpdateModule : IWorldUpdateModule {
    private static readonly (uint Id, ActionType Type)[] TrackedAutoCastItems =
    [
        (IDs.Item.HiCordial, ActionType.Item),
        (IDs.Item.HQCordial, ActionType.Item),
        (IDs.Item.Cordial, ActionType.Item),
        (IDs.Item.HQWateredCordial, ActionType.Item),
        (IDs.Item.WateredCordial, ActionType.Item),
    ];

    private readonly (uint Id, ActionType Type)[] _trackedFishingActions;
    private readonly Dictionary<uint, (float Time, int Stacks)> _statusScratch = [];
    private readonly Cooldown[] _cooldownScratch = new Cooldown[PlayerState.NumCooldownGroups];
    private readonly Dictionary<ulong, uint> _actionStatusScratch = [];
    private readonly Dictionary<ulong, int> _actionRecastScratch = [];
    private readonly Dictionary<uint, ushort> _dutyChargesScratch = [];

    private bool _needInventoryUpdate = true;

    public PlayerUpdateModule() {
        _trackedFishingActions = BuildTrackedFishingActions();
    }

    public void MarkInventoryDirty() => _needInventoryUpdate = true;

    public bool ConsumeInventoryDirty() {
        if (!_needInventoryUpdate)
            return false;
        _needInventoryUpdate = false;
        return true;
    }

    public unsafe void Update(WorldState ws) {
        var fwk = Framework.Instance();
        if (fwk != null)
            UpdateEorzeaTime(ws, fwk);

        UpdateStatuses(ws);
        UpdateCooldowns(ws);
        UpdateActionStates(ws);
        UpdateDutyActions(ws);
        UpdatePotCooldown(ws);
    }

    public void CollectAndApplyInventory(WorldState ws) {
        var (counts, stats) = CollectInventory();
        ws.Execute(counts);
        if (ws.Player.FreeInventorySlots != stats.FreeSlots || ws.Player.ReduceableFishCount != stats.ReduceableFish)
            ws.Execute(stats);
    }

    private static unsafe void UpdateEorzeaTime(WorldState ws, Framework* fwk) {
        var eorzea = TimeOnly.FromDateTime(DateTimeOffset.FromUnixTimeSeconds(fwk->ClientTime.EorzeaTime).DateTime);
        if (ws.EorzeaTime != eorzea)
            ws.Execute(new WorldState.OpEorzeaTime(eorzea));
    }

    private void UpdateStatuses(WorldState ws) {
        _statusScratch.Clear();
        if (Svc.Objects.LocalPlayer is { StatusList: var statuses }) {
            foreach (var buff in statuses)
                _statusScratch[buff.StatusId] = (buff.RemainingTime, buff.Param);
        }

        if (StatusesEqual(ws.Player.Statuses, _statusScratch))
            return;

        ws.Execute(new PlayerState.OpStatuses(new Dictionary<uint, (float, int)>(_statusScratch)));
    }

    private static bool StatusesEqual(Dictionary<uint, (float Time, int Stacks)> current, Dictionary<uint, (float Time, int Stacks)> next) {
        if (current.Count != next.Count)
            return false;

        foreach (var (id, (time, stacks)) in next) {
            if (!current.TryGetValue(id, out var prev))
                return false;
            if (prev.Stacks != stacks)
                return false;
            if (Math.Abs(prev.Time - time) > 1f)
                return false;
        }

        return true;
    }

    private unsafe void UpdateCooldowns(WorldState ws) {
        var am = ActionManager.Instance();
        if (am == null)
            return;

        var anyNonDefault = false;
        for (var i = 0; i < PlayerState.NumCooldownGroups; i++) {
            var detail = am->GetRecastGroupDetail(i);
            if (detail == null) {
                _cooldownScratch[i] = default;
                continue;
            }

            _cooldownScratch[i] = new Cooldown(detail->Elapsed, detail->Total);
            if (detail->Total > 0f)
                anyNonDefault = true;
        }

        if (!anyNonDefault && ws.Player.Cooldowns.All(c => c.Total <= 0f))
            return;

        if (MemoryExtensions.SequenceEqual(ws.Player.Cooldowns.AsSpan(), _cooldownScratch.AsSpan()))
            return;

        if (_cooldownScratch.AsSpan().IndexOfAnyExcept(default(Cooldown)) < 0) {
            ws.Execute(new PlayerState.OpCooldown(true, []));
            return;
        }

        var changes = CalcCooldownDifference(_cooldownScratch, ws.Player.Cooldowns);
        if (changes.Count > 0)
            ws.Execute(new PlayerState.OpCooldown(false, changes));
    }

    private static (uint Id, ActionType Type)[] BuildTrackedFishingActions()
        => [.. typeof(IDs.Actions).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Select(f => Convert.ToUInt32(f.GetValue(null) ?? 0u))
            .Where(id => id != IDs.Actions.None)
            .Select(id => (id, ActionType.Action))];

    private unsafe void UpdateActionStates(WorldState ws) {
        var am = ActionManager.Instance();
        if (am == null)
            return;

        _actionStatusScratch.Clear();
        _actionRecastScratch.Clear();
        foreach (var (id, type) in _trackedFishingActions.Concat(TrackedAutoCastItems)) {
            var key = PlayerState.ActionKey(type, id);
            _actionStatusScratch[key] = am->GetActionStatus(type, id);
            _actionRecastScratch[key] = am->GetRecastGroup((int)type, id);
        }

        if (ActionStatesEqual(ws.Player.ActionStatus, ws.Player.ActionRecastGroup, _actionStatusScratch, _actionRecastScratch))
            return;

        ws.Execute(new PlayerState.OpActionStates(new Dictionary<ulong, uint>(_actionStatusScratch), new Dictionary<ulong, int>(_actionRecastScratch)));
    }

    private static bool ActionStatesEqual(Dictionary<ulong, uint> currentStatus, Dictionary<ulong, int> currentGroups, Dictionary<ulong, uint> nextStatus, Dictionary<ulong, int> nextGroups) {
        if (currentStatus.Count != nextStatus.Count || currentGroups.Count != nextGroups.Count)
            return false;

        foreach (var (key, status) in nextStatus) {
            if (!currentStatus.TryGetValue(key, out var prevStatus) || prevStatus != status)
                return false;
            if (!currentGroups.TryGetValue(key, out var prevGroup) || prevGroup != nextGroups[key])
                return false;
        }

        return true;
    }

    private unsafe void UpdateDutyActions(WorldState ws) {
        _dutyChargesScratch.Clear();
        var dm = DutyActionManager.GetInstanceIfReady();
        var active = dm != null;
        if (dm != null) {
            for (var i = 0; i < dm->NumValidSlots; i++) {
                var id = dm->ActionId[i];
                if (id == 0)
                    continue;
                _dutyChargesScratch[id] = dm->CurCharges[i];
            }
        }

        if (ws.Player.DutyActionManagerActive == active && DutyChargesEqual(ws.Player.DutyActionCharges, _dutyChargesScratch))
            return;

        ws.Execute(new PlayerState.OpDutyActions(active, new Dictionary<uint, ushort>(_dutyChargesScratch)));
    }

    private static bool DutyChargesEqual(Dictionary<uint, ushort> current, Dictionary<uint, ushort> next) {
        if (current.Count != next.Count)
            return false;
        foreach (var (id, charges) in next) {
            if (!current.TryGetValue(id, out var prev) || prev != charges)
                return false;
        }
        return true;
    }

    private static List<(int, Cooldown)> CalcCooldownDifference(ReadOnlySpan<Cooldown> values, ReadOnlySpan<Cooldown> reference) {
        var res = new List<(int, Cooldown)>();
        for (var i = 0; i < Math.Min(values.Length, reference.Length); i++) {
            if (values[i] != reference[i])
                res.Add((i, values[i]));
        }
        return res;
    }

    private static unsafe void UpdatePotCooldown(WorldState ws) {
        var off = false;
        var am = ActionManager.Instance();
        if (am != null) {
            var recast = am->GetRecastGroupDetail(68);
            if (recast != null)
                off = recast->Total - recast->Elapsed <= 0;
        }

        if (ws.Player.IsPotOffCooldown == off)
            return;

        ws.Execute(new PlayerState.OpPotCooldown(off));
    }

    private static unsafe (PlayerState.OpItemCounts Counts, PlayerState.OpInventoryStats Stats) CollectInventory() {
        var dict = new Dictionary<uint, int>();
        var freeSlots = 0;
        var reduceableFish = 0;
        try {
            var inv = InventoryManager.Instance();
            if (inv != null) {
                for (var i = 0; i < 4; i++) {
                    var container = inv->GetInventoryContainer((InventoryType)i);
                    if (container == null) continue;
                    for (var k = 0; k < container->Size; k++) {
                        var slot = container->GetInventorySlot(k);
                        if (slot == null || slot->ItemId == 0) continue;
                        var kind = slot->Flags.HasFlag(InventoryItem.ItemFlags.HighQuality) ? ItemKind.Hq : slot->Flags.HasFlag(InventoryItem.ItemFlags.Collectable) ? ItemKind.Collectible : ItemKind.Normal;
                        var id = ItemUtil.GetRawId(slot->ItemId, kind);
                        dict[id] = dict.GetValueOrDefault(id, 0) + slot->Quantity;
                    }
                }

                foreach (var bag in InventoryType.Bags) {
                    foreach (var item in inv->GetInventoryItems(bag)) {
                        if (item.Value->ItemId == 0)
                            freeSlots++;
                        else if (IsReduceableFish(item))
                            reduceableFish++;
                    }
                }
            }
        }
        catch { }

        try {
            if (Player.Territory is { Value.TerritoryIntendedUse.RowId: 60 }) {
                var cosmopouch = ContentInventoryManager.Instance()->WKSInventoryProvider.Cosmopouch1;
                foreach (ref readonly var item in cosmopouch.WKSItems) {
                    if (item.WKSItemId == 0)
                        continue;
                    dict[item.WKSItemId] = item.WKSItemQuantity;
                }
            }
        }
        catch { }

        return (new PlayerState.OpItemCounts(dict), new PlayerState.OpInventoryStats(freeSlots, reduceableFish));
    }

    private static unsafe bool IsReduceableFish(Pointer<InventoryItem> item)
        => item.Value->Flags == InventoryItem.ItemFlags.Collectable && TryGetRow<Item>(item.Value->ItemId, out var row) && row.AetherialReduce > 0;
}
