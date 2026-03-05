using AutoHook.Conditions;
using AutoHook.Ui;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace AutoHook.Classes.AutoCasts;

public class AutoFishEyes : BaseActionCast
{
    public override int Priority { get; set; } = 6;
    public override bool IsExcludedPriority { get; set; } = false;

    [Obsolete("Legacy config. Replaced by ConditionSet.")] public bool OnlyWhenMakeShiftUp;

    public bool IgnoreMooch;

    public ConditionSet? ConditionSet { get; set; }

    public override bool DoesCancelMooch() => !IgnoreMooch;

    public override bool RequiresTimeWindow() => true;

    public AutoFishEyes() : base(UIStrings.Fish_Eyes, IDs.Actions.FishEyes, ActionType.Action) => HelpText = UIStrings.CancelsCurrentMooch;

    public override string GetName() => Name = UIStrings.Fish_Eyes;

    public override bool CastCondition()
    {
        if (ConditionSet is { Groups.Count: > 0 } &&
            !ConditionSet.Evaluate(Service.WorldState, Conditions.Conditions.Registry))
            return false;

        return !Service.WorldState.HasStatus(IDs.Status.FishEyes);
    }

    protected override DrawOptionsDelegate DrawOptions => () =>
    {
        DrawUtil.Checkbox(UIStrings.IgnoreMooch, ref IgnoreMooch, UIStrings.IgnoreMoochFishEyes);
        ConditionSet = ConditionUi.DrawConditionSet(UIStrings.Conditions, ConditionSet, ConditionScope.AutoCast, showPresets: true);
    };
}
