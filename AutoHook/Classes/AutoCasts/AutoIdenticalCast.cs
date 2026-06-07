using FFXIVClientStructs.FFXIV.Client.Game;

namespace AutoHook.Classes.AutoCasts;

public sealed class AutoIdenticalCast : BaseActionCast {
    public override bool DoesCancelMooch() => true;

    public AutoIdenticalCast() : base(IDs.Actions.IdenticalCast, ActionType.Action) { }

    public override string GetName() => UIStrings.Identical_Cast;

    public override string GetHelpText() => UIStrings.OverridesSurfaceSlap;

    public override bool CastCondition() => EvaluateConditionSet()
        && !Service.WorldState.HasStatus(IDs.Status.IdenticalCast)
        && !Service.WorldState.HasStatus(IDs.Status.SurfaceSlap);

    protected override DrawOptionsDelegate DrawOptions => () => {
        DrawAutoCastConditions();
        DrawUtil.Checkbox(UIStrings.Dont_Cancel_Mooch, ref DontCancelMooch, UIStrings.IdenticalCast_HelpText, true);
    };

    public override int Priority { get; set; } = 8;
    public override bool IsExcludedPriority { get; set; } = false;
}
