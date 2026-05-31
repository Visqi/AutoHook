using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using static AutoHook.Conditions.IConditionDefinition;

namespace AutoHook.Conditions.Definitions;

public sealed class FishCaughtCountCD : IConditionDefinition, ISimpleConditionValue<(bool Enabled, int Limit)> {
    public string Id => nameof(FishCaughtCountCD);
    public string Name => "Fish caught count";
    public ConditionScopeFlags AllowedScopes => ConditionScopeFlags.FishIgnore | ConditionScopeFlags.Hook;

    public bool Evaluate(WorldState world, IReadOnlyDictionary<string, object> parameters) {
        var fishId = GetInt(parameters, "id", 0);
        var args = GetIntCompareParams(parameters, defaultValue: 1);
        if (fishId <= 0)
            return args.Invert;

        var result = CompareInt(world.GetFishCaughtCount(fishId), args.Value, args.Op);
        return args.Apply(result);
    }

    public void DrawParams(Condition condition) {
        var fishId = GetInt(condition.Params, "id", 0);

        ImGui.SetNextItemWidth(70 * ImGuiHelpers.GlobalScale);
        if (ImGui.InputInt("Fish ID", ref fishId)) {
            fishId = Math.Max(0, fishId);
            condition.Params["id"] = (long)fishId;
        }

        ImGui.SameLine();
        DrawIntCompareParams(condition, "##fish_count_op", "Count", defaultValue: 1, clamp: v => Math.Max(1, v), valueWidth: 60);
    }

    (bool Enabled, int Limit) ISimpleConditionValue<(bool Enabled, int Limit)>.FromParams(IReadOnlyDictionary<string, object> p)
        => (true, Math.Max(1, GetInt(p, "val", 1)));

    IReadOnlyDictionary<string, object>? ISimpleConditionValue<(bool Enabled, int Limit)>.ToParams((bool Enabled, int Limit) value, object? context) {
        if (!value.Enabled) return null;
        var fishId = context is int id ? id : 0;
        var dict = new IntCompareParams(value.Limit, ">=", false).ToParams();
        if (fishId > 0)
            dict["id"] = (long)fishId;
        return dict;
    }
}
