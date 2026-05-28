using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using static AutoHook.Conditions.IConditionDefinition;

namespace AutoHook.Conditions.Definitions;

public sealed class OceanLastFishPointsCD : IConditionDefinition {
    public string Id => nameof(OceanLastFishPointsCD);
    public string Name => "Last ocean fish points";
    public string Category => "Fishing";
    public string Description => "Compares the points value of the last caught ocean fish in the current zone against a value.";
    public ConditionScopeFlags AllowedScopes => ConditionScopeFlags.Hook | ConditionScopeFlags.FishIgnore | ConditionScopeFlags.AutoCast;

    public bool Evaluate(WorldState world, IReadOnlyDictionary<string, object> parameters) {
        var points = GetLastOceanFishPointsValue(world);
        if (points == null) return GetBool(parameters, "inv", false);
        var val = GetInt(parameters, "val", 0);
        var op = GetOp(parameters, "op", ">=");
        var result = CompareInt(points.Value, val, op);
        return GetBool(parameters, "inv", false) ? !result : result;
    }

    public void DrawParams(Condition condition) {
        var val = GetInt(condition.Params, "val", 300);
        var op = condition.Params.TryGetValue("op", out var o) ? o?.ToString() ?? ">=" : ">=";
        var label = op is ">" or "<" or "<=" or "=" ? op : ">=";
        ImGui.SetNextItemWidth(50 * ImGuiHelpers.GlobalScale);
        using var combo = ImRaii.Combo("##ocean_points_op", label);
        if (combo) {
            foreach (var choice in new[] { ">", ">=", "<", "<=", "=" }) {
                var sel = choice == op;
                if (ImGui.Selectable(choice, sel))
                    condition.Params["op"] = choice;
            }
        }

        ImGui.SameLine();
        ImGui.SetNextItemWidth(80 * ImGuiHelpers.GlobalScale);
        if (ImGui.InputInt("Points", ref val))
            condition.Params["val"] = (long)Math.Max(0, val);
    }

    private static int? GetLastOceanFishPointsValue(WorldState w) {
        var of = w.OceanFishing;
        if (of.FishData == null || of.FishData.Count < 60) return null;
        var zone = (int)Math.Clamp(of.CurrentZone, 0, 2);
        var start = zone * 20;
        for (var i = start + 19; i >= start; i--) {
            var f = of.FishData[i];
            if (f.ItemId == 0) continue;
            var count = f.NqAmount + f.HqAmount;
            if (count == 0) return (int)f.TotalPoints;
            return (int)(f.TotalPoints / count);
        }
        return null;
    }
}
