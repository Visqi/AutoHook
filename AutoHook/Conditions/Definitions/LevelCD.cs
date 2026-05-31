using static AutoHook.Conditions.IConditionDefinition;

namespace AutoHook.Conditions.Definitions;

public sealed class LevelCD : IConditionDefinition {
    public string Id => nameof(LevelCD);
    public string Name => "Level";
    public string Category => "Player";
    public string Description => "Compares current class level against a value.";
    public ConditionScopeFlags AllowedScopes => ConditionScopeFlags.Hook | ConditionScopeFlags.AutoCordial | ConditionScopeFlags.AutoCast;

    public bool Evaluate(WorldState world, IReadOnlyDictionary<string, object> parameters) {
        var args = GetIntCompareParams(parameters);
        var result = CompareInt(world.Level, args.Value, args.Op);
        return args.Apply(result);
    }

    public void DrawParams(Condition condition)
        => DrawIntCompareParams(condition, "##level_op", "Level", clamp: v => Math.Clamp(v, 1, 100));
}
