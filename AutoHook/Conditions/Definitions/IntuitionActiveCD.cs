namespace AutoHook.Conditions.Definitions;

public sealed class IntuitionActiveCD : BoolInvertConditionDefinition {
    public override string Id => nameof(IntuitionActiveCD);
    public override string Name => "Fisher's Intuition";
    public override ConditionScopeFlags AllowedScopes => ConditionScopeFlags.All;
    public override bool SnapshottableOnCast => true;

    protected override bool ReadValue(WorldState world)
        => world.Fishing.Intuition.Status == IntuitionStatus.Active;

    protected override bool ReadSnapshotValue(CastInfoSnapshot snapshot)
        => snapshot.IntuitionStatus == IntuitionStatus.Active;
}
