using AutoHook.Ui;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace AutoHook.Classes.AutoCasts;

public class AutoSurfaceSlap : BaseActionCast {
    public override bool DoesCancelMooch() => true;

    public AutoSurfaceSlap() : base(UIStrings.Surface_Slap, IDs.Actions.SurfaceSlap, ActionType.Action)
        => HelpText = UIStrings.OverridesIdenticalCast;

    public override string GetName()
        => Name = UIStrings.Surface_Slap;

    public override bool CastCondition() => EvaluateConditionSet() && !Service.WorldState.HasStatus(IDs.Status.IdenticalCast) && !Service.WorldState.HasStatus(IDs.Status.SurfaceSlap);

    protected override DrawOptionsDelegate DrawOptions => () => {
        ConditionSet = ConditionUi.DrawConditionSetSlim(
            UIStrings.Conditions,
            ConditionSet,
            ConditionScope.AutoCast,
            showAdvanced: true,
            showSubPrefix: true);
        DrawUtil.Checkbox(UIStrings.Dont_Cancel_Mooch, ref DontCancelMooch, UIStrings.IdenticalCast_HelpText, true);
    };

    public override int Priority { get; set; } = 15;
    public override bool IsExcludedPriority { get; set; } = false;
}
