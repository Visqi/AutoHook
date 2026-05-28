using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using static AutoHook.Conditions.IConditionDefinition;

namespace AutoHook.Conditions.Definitions;

public sealed class BaitCountCD : IConditionDefinition {
    public string Id => nameof(BaitCountCD);
    public string Name => "Bait count";
    public string Category => "Counters";
    public string Description => "Compares the inventory count for a specific bait against a value.";
    public ConditionScopeFlags AllowedScopes => ConditionScopeFlags.Hook | ConditionScopeFlags.FishIgnore | ConditionScopeFlags.AutoCast;

    public bool Evaluate(WorldState world, IReadOnlyDictionary<string, object> parameters) {
        var baitId = GetInt(parameters, "id", 0);
        var val = GetInt(parameters, "val", 0);
        var op = GetOp(parameters, "op", ">=");
        var invert = GetBool(parameters, "inv", false);

        if (baitId <= 0)
            return invert;

        var total = world.GetItemCount((uint)baitId);

        var result = CompareInt(total, val, op);
        return invert ? !result : result;
    }

    public void DrawParams(Condition condition) {
        var baitId = GetInt(condition.Params, "id", 0);
        var val = GetInt(condition.Params, "val", 0);

        var currentBait = GameRes.Baits.FirstOrDefault(b => b.Id == baitId);
        var selectedName = currentBait is { Id: > 0 }
            ? $"[#{currentBait.Id}] {currentBait.Name}"
            : "-";

        DrawUtil.DrawComboSelector(
            GameRes.Baits,
            bait => $"[#{bait.Id}] {bait.Name}",
            selectedName,
            bait => {
                condition.Params["id"] = (long)bait.Id;
            });

        ImGui.SameLine();
        var op = condition.Params.TryGetValue("op", out var o) ? o?.ToString() ?? ">=" : ">=";
        var label = op is ">" or "<" or "<=" or "=" ? op : ">=";
        ImGui.SetNextItemWidth(50 * ImGuiHelpers.GlobalScale);
        using var combo = ImRaii.Combo("##baitcount_op", label);
        if (combo) {
            foreach (var choice in new[] { ">", ">=", "<", "<=", "=" }) {
                var sel = choice == op;
                if (ImGui.Selectable(choice, sel))
                    condition.Params["op"] = choice;
            }
        }

        ImGui.SameLine();
        ImGui.SetNextItemWidth(80 * ImGuiHelpers.GlobalScale);
        if (ImGui.InputInt("Count", ref val))
            condition.Params["val"] = (long)Math.Max(0, val);
    }
}

