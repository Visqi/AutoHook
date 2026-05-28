using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using static AutoHook.Conditions.IConditionDefinition;

namespace AutoHook.Conditions.Definitions;

public sealed class CosmicCurrentScoreCD : IConditionDefinition {
    public string Id => nameof(CosmicCurrentScoreCD);
    public string Name => "Cosmic current score";
    public string Category => "Fishing";
    public string Description => "Compares your current Cosmic Exploration score against a value.";
    public ConditionScopeFlags AllowedScopes => ConditionScopeFlags.Hook | ConditionScopeFlags.FishIgnore | ConditionScopeFlags.AutoCast;

    public bool Evaluate(WorldState world, IReadOnlyDictionary<string, object> parameters) {
        var val = GetInt(parameters, "val", 0);
        var op = GetOp(parameters, "op", ">=");
        var result = CompareInt((int)world.WKS.CurrentScore, val, op);
        return GetBool(parameters, "inv", false) ? !result : result;
    }

    public void DrawParams(Condition condition) {
        var val = GetInt(condition.Params, "val", 0);

        var op = condition.Params.TryGetValue("op", out var o) ? o?.ToString() ?? ">=" : ">=";
        var label = op is ">" or "<" or "<=" or "=" ? op : ">=";
        ImGui.SetNextItemWidth(50 * ImGuiHelpers.GlobalScale);
        using var combo = ImRaii.Combo("##cosmic_score_op", label);
        if (combo) {
            foreach (var choice in new[] { ">", ">=", "<", "<=", "=" }) {
                var sel = choice == op;
                if (ImGui.Selectable(choice, sel))
                    condition.Params["op"] = choice;
            }
        }

        ImGui.SameLine();
        ImGui.SetNextItemWidth(90 * ImGuiHelpers.GlobalScale);
        if (ImGui.InputInt("Score", ref val))
            condition.Params["val"] = (long)Math.Max(0, val);
    }
}
