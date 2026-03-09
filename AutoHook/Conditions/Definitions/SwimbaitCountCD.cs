using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using static AutoHook.Conditions.IConditionDefinition;

namespace AutoHook.Conditions.Definitions;

public sealed class SwimbaitCountCD : IConditionDefinition {
    public string Id => nameof(SwimbaitCountCD);
    public string Name => "Swimbait count";
    public string Category => "Fishing";
    public string Description => "Compares current swimbait count against a value.";
    public ConditionScopeFlags AllowedScopes => ConditionScopeFlags.Hook | ConditionScopeFlags.AutoCast;

    public bool Evaluate(WorldState world, IReadOnlyDictionary<string, object> parameters) {
        var val = GetInt(parameters, "val", 0);
        var op = GetOp(parameters, "op", ">=");
        var count = world.GetSwimbaitCount();
        var result = CompareInt(count, val, op);
        return GetBool(parameters, "inv", false) ? !result : result;
    }

    public void DrawParams(Condition condition) {
        var val = GetInt(condition.Params, "val", 0);
        ImGui.SetNextItemWidth(80 * ImGuiHelpers.GlobalScale);
        if (ImGui.InputInt("Swimbaits", ref val))
            condition.Params["val"] = (long)val;

        ImGui.SameLine();
        var op = condition.Params.TryGetValue("op", out var o) ? o?.ToString() ?? ">=" : ">=";
        var label = op is ">" or "<" or "<=" or "=" ? op : ">=";
        ImGui.SetNextItemWidth(50 * ImGuiHelpers.GlobalScale);
        using var combo = ImRaii.Combo("##swimbait_op", label);
        if (!combo) return;

        foreach (var choice in new[] { ">", ">=", "<", "<=", "=" }) {
            var sel = choice == op;
            if (ImGui.Selectable(choice, sel))
                condition.Params["op"] = choice;
        }
    }
}
