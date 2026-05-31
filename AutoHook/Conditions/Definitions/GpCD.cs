using static AutoHook.Conditions.IConditionDefinition;

namespace AutoHook.Conditions.Definitions;

public sealed class GpCD : IntCompareConditionDefinition {
    public override string Id => nameof(GpCD);
    public override string Name => "GP";
    public override ConditionScopeFlags AllowedScopes => ConditionScopeFlags.Hook | ConditionScopeFlags.AutoCordial | ConditionScopeFlags.AutoCast;
    protected override string ComboId => "##gp_op";
    protected override string ValueLabel => "GP";

    protected override int ReadValue(WorldState world, IReadOnlyDictionary<string, object> parameters)
        => (int)world.CurrentGp;
}
