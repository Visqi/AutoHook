using AutoHook.Ui;

namespace AutoHook.Classes.AutoCasts;

public sealed class AutoCastLine : BaseActionCast {
    public bool IgnoreMooch = true;

    public override bool DoesCancelMooch() => !IgnoreMooch;

    public override bool RequiresTimeWindow() => true;

    public AutoCastLine() : base(UIStrings.AutoCastLine_Auto_Cast_Line, IDs.Actions.Cast) {
        Enabled = true;
        Priority = 1;
    }

    public override int Priority { get; set; } = 0;

    public override bool IsExcludedPriority { get; set; } = true;

    public override bool CastCondition() => EvaluateConditionSet();

    protected override DrawOptionsDelegate DrawOptions => () => {
        DrawAutoCastConditions();

        DrawUtil.Checkbox(UIStrings.IgnoreMooch, ref IgnoreMooch,
            UIStrings.IgnoreMoochHelpText);
    };
}
