using FFXIVClientStructs.FFXIV.Client.Game;

namespace AutoHook.Classes.AutoCasts;

public sealed class AutoSurfaceSlap : BaseActionCast {
    public override bool DoesCancelMooch() => true;

    public AutoSurfaceSlap() : base(IDs.Actions.SurfaceSlap, ActionType.Action) { }

    public override string GetName() => UIStrings.Surface_Slap;

    public override string GetHelpText() => UIStrings.OverridesIdenticalCast;

    public override bool CastCondition() => EvaluateConditionSet()
        && !Service.WorldState.HasStatus(IDs.Status.IdenticalCast)
        && !Service.WorldState.HasStatus(IDs.Status.SurfaceSlap);

    protected override DrawOptionsDelegate DrawOptions => () => {
        DrawAutoCastConditions();
        DrawUtil.Checkbox(UIStrings.Dont_Cancel_Mooch, ref DontCancelMooch, UIStrings.IdenticalCast_HelpText, true);
    };

    public override int Priority { get; set; } = 15;
    public override bool IsExcludedPriority { get; set; } = false;
}
