using static AutoHook.Conditions.IConditionDefinition;

namespace AutoHook.Conditions.Definitions;

public sealed class IntuitionTimerCD : IConditionDefinition {
    public string Id => nameof(IntuitionTimerCD);
    public string Name => "Intuition time";
    public string Category => "Fishing";
    public string Description => "Compares remaining Fisher's Intuition time against a value.";
    public ConditionScopeFlags AllowedScopes => ConditionScopeFlags.Hook | ConditionScopeFlags.FishIgnore | ConditionScopeFlags.AutoCast;

    public bool Evaluate(WorldState world, IReadOnlyDictionary<string, object> parameters) {
        var args = GetIntCompareParams(parameters, "sec");
        if (world.Fishing.Intuition.Status != IntuitionStatus.Active) return args.Invert;
        var lhs = (int)Math.Floor(world.Fishing.Intuition.TimeRemaining);
        var result = CompareInt(lhs, args.Value, args.Op);
        return args.Apply(result);
    }

    public void DrawParams(Condition condition)
        => DrawIntCompareParams(condition, "##intu_op", "Seconds", valueKey: "sec", clamp: v => Math.Max(0, v));
}
