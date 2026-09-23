using System.ComponentModel;

namespace AutoHook.Classes.AutoCasts;

public sealed class AutoElectricCurrent : BaseActionCast {
    public AutoElectricCurrent(bool isSpearfishing = true) : base(IDs.Actions.ElectricCurrent) {
        IsSpearFishing = isSpearfishing;
        GpThreshold = 0;
    }

    public override string GetName() => UIStrings.ElectricCurrent;

    public override bool ShowGpThreshold => false;

    public override bool CastCondition() => EvaluateConditionSet();

    protected override DrawOptionsDelegate DrawOptions => () => DrawAutoCastConditions();

    [DefaultValue(true)]
    public override bool IsExcludedPriority { get; set; } = true;
}
