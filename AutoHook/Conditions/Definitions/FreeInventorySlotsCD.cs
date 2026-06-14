using FFXIVClientStructs.FFXIV.Client.Game;

namespace AutoHook.Conditions.Definitions;

public sealed class FreeInventorySlotsCD : IntCompareConditionDefinition {
    public override string Id => nameof(FreeInventorySlotsCD);
    public override string Name => "Free inventory slots";
    public override ConditionScopeFlags AllowedScopes => ConditionScopeFlags.Hook | ConditionScopeFlags.FishIgnore | ConditionScopeFlags.AutoCast;
    protected override string ComboId => "##freeinventory_op";
    protected override string ValueLabel => "Slots";
    protected override Func<int, int>? Clamp => static v => Math.Max(0, v);

    protected override int ReadValue(WorldState world, IReadOnlyDictionary<string, object> parameters)
        => CountFreeInventorySlots();

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
