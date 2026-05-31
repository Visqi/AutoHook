using FFXIVClientStructs.FFXIV.Client.Game;
using static AutoHook.Conditions.IConditionDefinition;

namespace AutoHook.Conditions.Definitions;

public sealed class FreeInventorySlotsCD : IConditionDefinition {
    public string Id => nameof(FreeInventorySlotsCD);
    public string Name => "Free inventory slots";
    public string Category => "Inventory";
    public string Description => "Compares the number of empty slots in main inventory (bags 1–4).";
    public ConditionScopeFlags AllowedScopes => ConditionScopeFlags.Hook | ConditionScopeFlags.FishIgnore | ConditionScopeFlags.AutoCast;

    public bool Evaluate(WorldState world, IReadOnlyDictionary<string, object> parameters) {
        var args = GetIntCompareParams(parameters);
        var result = CompareInt(CountFreeInventorySlots(), args.Value, args.Op);
        return args.Apply(result);
    }

    public void DrawParams(Condition condition)
        => DrawIntCompareParams(condition, "##freeinventory_op", "Slots", clamp: v => Math.Max(0, v));

    private static unsafe int CountFreeInventorySlots() {
        var inv = InventoryManager.Instance();
        if (inv == null)
            return 0;

        ref var manager = ref *inv;
        var count = 0;
        foreach (var bag in InventoryType.Bags) {
            foreach (var item in manager.GetInventoryItems(bag)) {
                if (item.Value->ItemId == 0)
                    count++;
            }
        }

        return count;
    }
}
