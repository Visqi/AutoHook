using AutoHook.Ui;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace AutoHook.Classes.AutoCasts;

public class AutoFishEyes : BaseActionCast
{
    public override int Priority { get; set; } = 6;
    public override bool IsExcludedPriority { get; set; } = false;

    [Obsolete("Legacy config. Replaced by ConditionSet.")] public bool OnlyWhenMakeShiftUp;

    public bool IgnoreMooch;

    public override bool DoesCancelMooch() => !IgnoreMooch;

    public override bool RequiresTimeWindow() => true;

    public AutoFishEyes() : base(UIStrings.Fish_Eyes, IDs.Actions.FishEyes, ActionType.Action) => HelpText = UIStrings.CancelsCurrentMooch;

    public override string GetName() => Name = UIStrings.Fish_Eyes;

    public override bool CastCondition() => EvaluateConditionSet() && !Service.WorldState.HasStatus(IDs.Status.FishEyes);

    protected override DrawOptionsDelegate DrawOptions => () =>
    {
        DrawUtil.Checkbox(UIStrings.IgnoreMooch, ref IgnoreMooch, UIStrings.IgnoreMoochFishEyes);
        ConditionSet = ConditionUi.DrawConditionSetSlim(
            UIStrings.Conditions,
            ConditionSet,
            ConditionScope.AutoCast,
            showAdvanced: true,
            showSubPrefix: true);
    };
}
