using System.ComponentModel;

namespace AutoHook.Classes.AutoCasts;

public sealed class AutoBaitedBreath : BaseActionCast {
    public AutoBaitedBreath(bool isSpearfishing = true) : base(IDs.Actions.BaitedBreath) {
        IsSpearFishing = isSpearfishing;
        GpThreshold = 300;
    }

    public override string GetName() => UIStrings.BaitedBreath;

    public override bool CastCondition() => EvaluateConditionSet();

    protected override DrawOptionsDelegate DrawOptions => () => DrawAutoCastConditions();

    [DefaultValue(true)]
    public override bool IsExcludedPriority { get; set; } = true;
}
