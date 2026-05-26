using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
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
        var val = GetInt(parameters, "val", 0);
        var op = GetOp(parameters, "op", ">=");
        var invert = GetBool(parameters, "inv", false);

        var count = CountFreeInventorySlots();
        var result = CompareInt(count, val, op);
        return invert ? !result : result;
    }

    public void DrawParams(Condition condition) {
        var val = GetInt(condition.Params, "val", 0);

        ImGui.SetNextItemWidth(80 * ImGuiHelpers.GlobalScale);
        if (ImGui.InputInt("Slots", ref val))
            condition.Params["val"] = (long)Math.Max(0, val);

        ImGui.SameLine();
        var op = condition.Params.TryGetValue("op", out var o) ? o?.ToString() ?? ">=" : ">=";
        var label = op is ">" or "<" or "<=" or "=" ? op : ">=";
        ImGui.SetNextItemWidth(50 * ImGuiHelpers.GlobalScale);
        using var combo = ImRaii.Combo("##freeinventory_op", label);
        if (!combo) return;

        foreach (var choice in new[] { ">", ">=", "<", "<=", "=" }) {
            var sel = choice == op;
            if (ImGui.Selectable(choice, sel))
                condition.Params["op"] = choice;
        }
    }

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
