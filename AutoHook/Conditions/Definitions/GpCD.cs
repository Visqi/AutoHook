using static AutoHook.Conditions.IConditionDefinition;

namespace AutoHook.Conditions.Definitions;

public sealed class GpCD : IConditionDefinition {
    public string Id => nameof(GpCD);
    public string Name => "GP";
    public string Category => "Player";
    public string Description => "Compares current GP against a value.";
    public ConditionScopeFlags AllowedScopes => ConditionScopeFlags.Hook | ConditionScopeFlags.AutoCordial | ConditionScopeFlags.AutoCast;

    public bool Evaluate(WorldState world, IReadOnlyDictionary<string, object> parameters) {
        var args = GetIntCompareParams(parameters);
        var result = CompareInt((int)world.CurrentGp, args.Value, args.Op);
        return args.Invert ? !result : result;
    }

    public void DrawParams(Condition condition)
        => DrawIntCompareParams(condition, "##gp_op", "GP");
}
