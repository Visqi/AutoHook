namespace AutoHook.Conditions.Definitions;

public sealed class MaxGpCD : IntCompareConditionDefinition {
    public override string Id => nameof(MaxGpCD);
    public override string Name => "Max GP";
    public override ConditionScopeFlags AllowedScopes => ConditionScopeFlags.Hook | ConditionScopeFlags.AutoCordial | ConditionScopeFlags.AutoCast;
    protected override string ComboId => "##maxgp_op";
    protected override string ValueLabel => "Max GP";

    protected override int ReadValue(WorldState world, IReadOnlyDictionary<string, object> parameters)
        => (int)world.MaxGp;
}
