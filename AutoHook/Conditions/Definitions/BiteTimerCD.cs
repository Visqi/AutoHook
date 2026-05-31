using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace AutoHook.Conditions.Definitions;

public sealed class BiteTimerCD : IConditionDefinition {
    public string Id => nameof(BiteTimerCD);
    public string Name => "Bite timer";
    public string Category => "Time";
    public string Description => "Checks current bite timer (seconds since bite) against one or more ranges.";
    public ConditionScopeFlags AllowedScopes => ConditionScopeFlags.Hook | ConditionScopeFlags.AutoCast;

    public bool Evaluate(WorldState world, IReadOnlyDictionary<string, object> parameters) {
        var args = IConditionDefinition.GetRangeParams(parameters);
        var ranges = args.Ranges;
        if (ranges.Count == 0) return true;
        var t = world.Fishing.BiteInfo.BiteTimeSeconds;
        var result = false;
        foreach (var (min, max) in ranges)
            if (t >= min && (max <= 0 || t <= max)) { result = true; break; }
        return args.Apply(result);
    }

    public void DrawParams(Condition condition) {
        var args = IConditionDefinition.GetRangeParams(condition.Params);
        var ranges = args.Ranges;
        var min = ranges.Count > 0 ? ranges[0].Min : 0;
        var max = ranges.Count > 0 ? ranges[0].Max : 0;

        ImGui.SetNextItemWidth(80 * ImGuiHelpers.GlobalScale);
        if (ImGui.InputDouble("Min", ref min, 0.1, 1, "%.1f")) {
            var list = ranges.Count == 0 ? new List<(double, double)>() : [.. ranges];
            if (list.Count == 0)
                list.Add((min, max));
            else
                list[0] = (min, max);
            args = args with { Ranges = list };
            condition.Params = args.ToParams();
        }

        ImGui.SameLine();
        ImGui.SetNextItemWidth(80 * ImGuiHelpers.GlobalScale);
        if (ImGui.InputDouble("Max (0 = no cap)", ref max, 0.1, 1, "%.1f")) {
            var list = ranges.Count == 0 ? new List<(double, double)>() : [.. ranges];
            if (list.Count == 0)
                list.Add((min, max));
            else
                list[0] = (min, max);
            args = args with { Ranges = list };
            condition.Params = args.ToParams();
        }
    }
}
