using System.ComponentModel;

namespace AutoHook.Classes.AutoCasts;

public sealed class AutoCollect : BaseActionCast {
    [DefaultValue(2)]
    public override int Priority { get; set; } = 2;
    [DefaultValue(true)]
    public override bool IsExcludedPriority { get; set; } = true;

    public AutoCollect(bool isSpearfishing = false) : base(IDs.Actions.Collect) => IsSpearFishing = isSpearfishing;

    public override string GetName() => UIStrings.Collect;

    public override string GetHelpText() => UIStrings.CollectHelpText;
    public override bool ShowGpThreshold => false;

    public override bool CastCondition()
        => EvaluateConditionSet() && !Service.WorldState.Player.HasStatus(IDs.Status.CollectorsGlove);

    protected override DrawOptionsDelegate DrawOptions => () => DrawAutoCastConditions();
}
