using System.ComponentModel;

namespace AutoHook.Presets.AutoCasts;

public sealed class AutoCollect : BaseActionCast {
    [DefaultValue(2)]
    public override int Priority { get; set; } = 2;
    [DefaultValue(true)]
    public override bool IsExcludedPriority { get; set; } = true;

    public AutoCollect(bool isSpearfishing = false) : base(IDs.Actions.Collect) => IsSpearFishing = isSpearfishing;

    public override string GetName() => UIStrings.Collect;

    public override string GetHelpText() => UIStrings.CollectHelpText;
    public override bool ShowGpThreshold => false;

    public override bool CastCondition(WorldState ws)
        => EvaluateConditionSet(ws) && !ws.Player.HasStatus(IDs.Status.CollectorsGlove);

    protected override DrawOptionsDelegate DrawOptions => () => DrawAutoCastConditions();
}
