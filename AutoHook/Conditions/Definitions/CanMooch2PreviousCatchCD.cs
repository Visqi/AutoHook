namespace AutoHook.Conditions.Definitions;

public sealed class CanMooch2PreviousCatchCD : BoolInvertConditionDefinition {
    public override string Id => nameof(CanMooch2PreviousCatchCD);
    public override string Name => "Mooch II Available";
    public override ConditionScopeFlags AllowedScopes => ConditionScopeFlags.Hook | ConditionScopeFlags.AutoCast;

    protected override bool ReadValue(WorldState world)
        => world.Fishing.PreviousCatch.CanMooch2PreviousCatch;
}
