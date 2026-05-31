using static AutoHook.Conditions.IConditionDefinition;

namespace AutoHook.Conditions.Definitions;

public sealed class MaxGpCD : IConditionDefinition {
    public string Id => nameof(MaxGpCD);
    public string Name => "Max GP";
    public string Category => "Player";
    public string Description => "Compares maximum GP against a value.";
    public ConditionScopeFlags AllowedScopes => ConditionScopeFlags.Hook | ConditionScopeFlags.AutoCordial | ConditionScopeFlags.AutoCast;

    public bool Evaluate(WorldState world, IReadOnlyDictionary<string, object> parameters) {
        var args = GetIntCompareParams(parameters);
        var result = CompareInt((int)world.MaxGp, args.Value, args.Op);
        return args.Invert ? !result : result;
    }

    public void DrawParams(Condition condition)
        => DrawIntCompareParams(condition, "##maxgp_op", "Max GP");
}
