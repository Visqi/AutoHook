using AutoHook.Ui;
using System.ComponentModel;

namespace AutoHook.Classes.AutoCasts;

public class AutoCastLine : BaseActionCast {
    [Obsolete("Legacy config")] public bool OnlyCastWithFishEyes = false;
    [Obsolete("Legacy config")] public bool OnlyCastLarge = false;

    [DefaultValue(true)] public bool IgnoreMooch = true;

    public override bool DoesCancelMooch() => !IgnoreMooch;

    public override bool RequiresTimeWindow() => true;

    public AutoCastLine() : base(UIStrings.AutoCastLine_Auto_Cast_Line, IDs.Actions.Cast) {
        Enabled = true;
        Priority = 1;
    }

    public override int Priority { get; set; } = 0;

    public override bool IsExcludedPriority { get; set; } = true;

    public override bool CastCondition() => EvaluateConditionSet();

    public override string GetName() => Name = UIStrings.AutoCastLine_Auto_Cast_Line;

    protected override DrawOptionsDelegate DrawOptions => () => {
        ConditionSet = ConditionUi.DrawConditionSetSlim(
            UIStrings.Conditions,
            ConditionSet,
            ConditionScope.AutoCast,
            showAdvanced: true,
            showSubPrefix: true);

        DrawUtil.Checkbox(UIStrings.IgnoreMooch, ref IgnoreMooch,
            UIStrings.IgnoreMoochHelpText);
    };
}
