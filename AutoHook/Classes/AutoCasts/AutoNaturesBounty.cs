using System.ComponentModel;

namespace AutoHook.Classes.AutoCasts;

public sealed class AutoNaturesBounty : BaseActionCast {
    public AutoNaturesBounty(bool isSpearfishing = true) : base(IDs.Actions.NaturesBounty) {
        IsSpearFishing = isSpearfishing;
        GpThreshold = 100;
    }

    public override string GetName() => UIStrings.Use_Natures_Bounty;

    public override bool CastCondition()
        => EvaluateConditionSet() && !Service.WorldState.Player.HasStatus(IDs.Status.NaturesBounty);

    protected override DrawOptionsDelegate DrawOptions => () => DrawAutoCastConditions();

    [DefaultValue(true)]
    public override bool IsExcludedPriority { get; set; } = true;
}
