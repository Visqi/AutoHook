namespace AutoHook.Data;

public sealed class PlayerInfo {
    public uint CurrentGp;
    public uint MaxGp;
    public byte Level;

    public readonly Dictionary<uint, (float Time, int Stacks)> Statuses = [];
    public readonly Dictionary<uint, int> ItemCounts = [];

    public bool BlockCasting;
    public bool IsPotOffCooldown;

    public int GetItemCount(uint itemId) => ItemCounts.TryGetValue(itemId, out var c) ? c : 0;
    public bool HasItem(uint itemId) => GetItemCount(itemId) > 0;
    public bool HaveCordialInInventory(uint id) => HasItem(id);

    public bool HasStatus(uint statusId) => Statuses.ContainsKey(statusId);
    public float GetStatusTime(uint statusId) => Statuses.TryGetValue(statusId, out var t) ? t.Time : 0f;
    public int GetStatusStacks(uint statusId) => Statuses.TryGetValue(statusId, out var s) ? s.Stacks : 0;

    public bool HasAnyStatus(uint[] statusIds) {
        foreach (var id in statusIds)
            if (HasStatus(id)) return true;
        return false;
    }

    // Generate a set of operations that would turn default-constructed state into current state.
    public IEnumerable<WorldState.Operation> CompareToInitial() {
        if (CurrentGp != 0 || MaxGp != 0)
            yield return new OpGp(CurrentGp, MaxGp);
        if (Level != 0)
            yield return new OpLevel(Level);
        if (Statuses.Count != 0)
            yield return new OpStatuses(Statuses);
        if (ItemCounts.Count != 0)
            yield return new OpItemCounts(ItemCounts);
        if (IsPotOffCooldown)
            yield return new OpPotCooldown(IsPotOffCooldown);
        if (BlockCasting)
            yield return new OpSetBlockCasting(BlockCasting);
    }

    public sealed record OpGp(uint Current, uint Max) : WorldState.Operation {
        protected override void Exec(WorldState ws) {
            ws.Player.CurrentGp = Current;
            ws.Player.MaxGp = Max;
        }
    }

    public sealed record OpLevel(byte Level) : WorldState.Operation {
        protected override void Exec(WorldState ws) => ws.Player.Level = Level;
    }

    public sealed record OpStatuses(IReadOnlyDictionary<uint, (float Time, int Stacks)> Statuses) : WorldState.Operation {
        protected override void Exec(WorldState ws) {
            ws.Player.Statuses.Clear();
            foreach (var kv in Statuses)
                ws.Player.Statuses[kv.Key] = kv.Value;
        }
    }

    public sealed record OpSetBlockCasting(bool Block) : WorldState.Operation {
        protected override void Exec(WorldState ws) => ws.Player.BlockCasting = Block;
    }

    public sealed record OpItemCounts(IReadOnlyDictionary<uint, int> Counts) : WorldState.Operation {
        protected override void Exec(WorldState ws) {
            ws.Player.ItemCounts.Clear();
            foreach (var kv in Counts)
                ws.Player.ItemCounts[kv.Key] = kv.Value;
        }
    }

    public sealed record OpPotCooldown(bool OffCooldown) : WorldState.Operation {
        protected override void Exec(WorldState ws) => ws.Player.IsPotOffCooldown = OffCooldown;
    }
}
