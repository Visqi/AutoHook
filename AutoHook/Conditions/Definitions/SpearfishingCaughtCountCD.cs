using Dalamud.Bindings.ImGui;
using static AutoHook.Conditions.IConditionDefinition;

namespace AutoHook.Conditions.Definitions;

public sealed class SpearfishingCaughtCountCD : IConditionDefinition {
    public string Id => nameof(SpearfishingCaughtCountCD);
    public string Name => "Spearfishing caught count";
    public ConditionScopeFlags AllowedScopes => ConditionScopeFlags.Spearfishing;

    public bool Evaluate(WorldState world, IReadOnlyDictionary<string, object> parameters) {
        var fishId = GetUInt(parameters, "id", 0);
        var args = GetIntCompareParams(parameters, defaultValue: 1);
        if (fishId == 0)
            return args.Invert;

        var result = CompareInt(world.Spearfishing.GetFishCaughtCount(fishId), args.Value, args.Op);
        return args.Apply(result);
    }

    public void DrawParams(Condition condition) {
        var fishId = GetInt(condition.Params, "id", 0);
        var currentFish = GameRes.SpearfishFishes.FirstOrDefault(fish => fish.ItemId == fishId);
        var selectedName = currentFish is not null ? $"[#{currentFish.ItemId}] {currentFish.Name}" : "-";

        DrawUtil.DrawComboSelector(GameRes.SpearfishFishes, fish => $"[#{fish.ItemId}] {fish.Name}", selectedName, fish => condition.Params["id"] = (long)fish.ItemId);
        ImGui.SameLine();
        DrawIntCompareParams(condition, "##spearfishing_caught_op", "Count", defaultValue: 1, clamp: value => Math.Max(1, value), valueWidth: 60);
    }

    public string DescribeParameters(IReadOnlyDictionary<string, object> parameters)
        => ConditionParameterFormat.FormatFishCount(parameters);
}
