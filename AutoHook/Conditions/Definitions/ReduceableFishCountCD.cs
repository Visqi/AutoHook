using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.Interop;
using Lumina.Excel.Sheets;
using static AutoHook.Conditions.IConditionDefinition;

namespace AutoHook.Conditions.Definitions;

public sealed class ReduceableFishCountCD : IConditionDefinition {
    public string Id => nameof(ReduceableFishCountCD);
    public string Name => "Reduceable fish count";
    public string Category => "Inventory";
    public string Description => "Compares the number of collectable fish in main inventory that can be aetherially reduced.";
    public ConditionScopeFlags AllowedScopes => ConditionScopeFlags.Hook | ConditionScopeFlags.FishIgnore | ConditionScopeFlags.AutoCast;

    public bool Evaluate(WorldState world, IReadOnlyDictionary<string, object> parameters) {
        var args = GetIntCompareParams(parameters);
        var result = CompareInt(CountReduceableFish(), args.Value, args.Op);
        return args.Apply(result);
    }

    public void DrawParams(Condition condition)
        => DrawIntCompareParams(condition, "##reduceablefish_op", "Fish", clamp: v => Math.Max(0, v));

    private static unsafe int CountReduceableFish() {
        var inv = InventoryManager.Instance();
        if (inv == null)
            return 0;

        ref var manager = ref *inv;
        var count = 0;
        foreach (var bag in InventoryType.Bags) {
            foreach (var item in manager.GetInventoryItems(bag)) {
                if (IsPurifyable(item))
                    count++;
            }
        }

        return count;
    }

    private static unsafe bool IsPurifyable(Pointer<InventoryItem> item)
        => item.Value->ItemId != 0
            && item.Value->Flags == InventoryItem.ItemFlags.Collectable
            && TryGetRow<Item>(item.Value->ItemId, out var row)
            && row.AetherialReduce > 0;
}
