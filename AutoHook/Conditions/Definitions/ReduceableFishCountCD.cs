using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
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
        var val = GetInt(parameters, "val", 0);
        var op = GetOp(parameters, "op", ">=");
        var invert = GetBool(parameters, "inv", false);

        var count = CountReduceableFish();
        var result = CompareInt(count, val, op);
        return invert ? !result : result;
    }

    public void DrawParams(Condition condition) {
        var val = GetInt(condition.Params, "val", 0);

        ImGui.SetNextItemWidth(80 * ImGuiHelpers.GlobalScale);
        if (ImGui.InputInt("Fish", ref val))
            condition.Params["val"] = (long)Math.Max(0, val);

        ImGui.SameLine();
        var op = condition.Params.TryGetValue("op", out var o) ? o?.ToString() ?? ">=" : ">=";
        var label = op is ">" or "<" or "<=" or "=" ? op : ">=";
        ImGui.SetNextItemWidth(50 * ImGuiHelpers.GlobalScale);
        using var combo = ImRaii.Combo("##reduceablefish_op", label);
        if (!combo) return;

        foreach (var choice in new[] { ">", ">=", "<", "<=", "=" }) {
            var sel = choice == op;
            if (ImGui.Selectable(choice, sel))
                condition.Params["op"] = choice;
        }
    }

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
