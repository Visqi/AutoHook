using System.ComponentModel;

namespace AutoHook.Classes.AutoCasts;

public sealed class AutoVeteranTrade : BaseActionCast {
    public AutoVeteranTrade(bool isSpearfishing = true) : base(IDs.Actions.VeteranTrade) {
        IsSpearFishing = isSpearfishing;
        GpThreshold = 200;
    }

    public override string GetName() => UIStrings.VeteranTrade;

    public override bool CastCondition()
        => EvaluateConditionSet() && !Service.WorldState.Player.HasStatus(IDs.Status.VeteranTrade);

    protected override DrawOptionsDelegate DrawOptions => () => DrawAutoCastConditions();

    [DefaultValue(true)]
    public override bool IsExcludedPriority { get; set; } = true;
}
