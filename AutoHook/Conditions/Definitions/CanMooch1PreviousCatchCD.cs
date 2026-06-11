namespace AutoHook.Conditions.Definitions;

public sealed class CanMooch1PreviousCatchCD : BoolInvertConditionDefinition {
    public override string Id => nameof(CanMooch1PreviousCatchCD);
    public override string Name => "Mooch I Available";
    public override ConditionScopeFlags AllowedScopes => ConditionScopeFlags.Hook | ConditionScopeFlags.AutoCast;

    protected override bool ReadValue(WorldState world)
        => world.Fishing.PreviousCatch.CanMoochPreviousCatch;
}
