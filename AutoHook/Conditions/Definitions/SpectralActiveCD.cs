using static AutoHook.Conditions.IConditionDefinition;

namespace AutoHook.Conditions.Definitions;

public sealed class SpectralActiveCD : BoolInvertConditionDefinition {
    public override string Id => nameof(SpectralActiveCD);
    public override string Name => "Spectral current";
    public override ConditionScopeFlags AllowedScopes => ConditionScopeFlags.Hook | ConditionScopeFlags.FishIgnore | ConditionScopeFlags.AutoCast;

    protected override bool ReadValue(WorldState world)
        => world.SpectralCurrentStatus == SpectralCurrentStatus.Active;
}
