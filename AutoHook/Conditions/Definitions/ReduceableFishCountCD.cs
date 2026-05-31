using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.Interop;
using Lumina.Excel.Sheets;
using static AutoHook.Conditions.IConditionDefinition;

namespace AutoHook.Conditions.Definitions;

public sealed class ReduceableFishCountCD : IntCompareConditionDefinition {
    public override string Id => nameof(ReduceableFishCountCD);
    public override string Name => "Reduceable fish count";
    public override ConditionScopeFlags AllowedScopes => ConditionScopeFlags.Hook | ConditionScopeFlags.FishIgnore | ConditionScopeFlags.AutoCast;
    protected override string ComboId => "##reduceablefish_op";
    protected override string ValueLabel => "Fish";
    protected override Func<int, int>? Clamp => static v => Math.Max(0, v);

    protected override int ReadValue(WorldState world, IReadOnlyDictionary<string, object> parameters)
        => CountReduceableFish();

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
