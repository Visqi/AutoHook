using static AutoHook.Conditions.IConditionDefinition;

namespace AutoHook.Conditions.Definitions;

public sealed class CosmicCollectedTotalCD : IConditionDefinition {
    public string Id => nameof(CosmicCollectedTotalCD);
    public string Name => "Cosmic collected total";
    public string Category => "Fishing";
    public string Description => "Compares your Cosmic Exploration collected total against a value.";
    public ConditionScopeFlags AllowedScopes => ConditionScopeFlags.Hook | ConditionScopeFlags.FishIgnore | ConditionScopeFlags.AutoCast;

    public bool Evaluate(WorldState world, IReadOnlyDictionary<string, object> parameters) {
        var args = GetIntCompareParams(parameters);
        var result = CompareInt(world.WKS.CollectedTotal, args.Value, args.Op);
        return args.Invert ? !result : result;
    }

    public void DrawParams(Condition condition)
        => DrawIntCompareParams(condition, "##cosmic_collected_total_op", "Collected", clamp: v => Math.Max(0, v), valueWidth: 90);
}
