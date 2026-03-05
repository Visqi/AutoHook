using AutoHook.Conditions;
using AutoHook.Ui;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace AutoHook.Classes.AutoCasts;

public class AutoSurfaceSlap : BaseActionCast
{
    public ConditionSet? ConditionSet { get; set; }

    public override bool DoesCancelMooch() => true;

    public AutoSurfaceSlap() : base(UIStrings.Surface_Slap, IDs.Actions.SurfaceSlap, ActionType.Action)
        => HelpText = UIStrings.OverridesIdenticalCast;

    public override string GetName()
        => Name = UIStrings.Surface_Slap;

    public override bool CastCondition()
    {
        if (ConditionSet is { Groups.Count: > 0 } &&
            !ConditionSet.Evaluate(Service.WorldState, Conditions.Conditions.Registry))
            return false;

        return !Service.WorldState.HasStatus(IDs.Status.IdenticalCast) && !Service.WorldState.HasStatus(IDs.Status.SurfaceSlap);
    }

    protected override DrawOptionsDelegate DrawOptions => () =>
    {
        if (DrawUtil.Checkbox(UIStrings.Dont_Cancel_Mooch, ref DontCancelMooch, UIStrings.IdenticalCast_HelpText, true))
            Service.Save();
        ConditionSet = ConditionUi.DrawConditionSet(UIStrings.Conditions, ConditionSet, ConditionScope.AutoCast, showPresets: true);
    };

    public override int Priority { get; set; } = 15;
    public override bool IsExcludedPriority { get; set; } = false;
}
