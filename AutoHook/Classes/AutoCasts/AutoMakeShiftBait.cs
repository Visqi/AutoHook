using AutoHook.Ui;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace AutoHook.Classes.AutoCasts;

public class AutoMakeShiftBait : BaseActionCast
{
    public int MakeshiftBaitStacks = 5;

    [Obsolete("Legacy config. Replaced by ConditionSet.")] public bool _onlyUseWithIntuition;
    [Obsolete("Legacy config. Replaced by ConditionSet.")] public bool OnlyWhenMoochNotUp;
    [Obsolete("Legacy config. Replaced by ConditionSet.")] public bool UseOnlyWhenMoochIIOnCD;

    public override bool RequiresTimeWindow() => true;

    public AutoMakeShiftBait() : base(UIStrings.MakeShift_Bait, IDs.Actions.MakeshiftBait, ActionType.Action)
        => HelpText = UIStrings.TabAutoCasts_DrawMakeShiftBait_HelpText;

    public override string GetName()
        => Name = UIStrings.MakeShift_Bait;

    public override bool CastCondition()
    {
        if (!EvaluateConditionSet())
            return false;

        if (Service.WorldState.HasStatus(IDs.Status.MakeshiftBait))
            return false;

        if (Service.WorldState.HasStatus(IDs.Status.PrizeCatch))
            return false;

        var available = Service.WorldState.ActionAvailable(IDs.Actions.MakeshiftBait);
        var hasStacks = Service.WorldState.HasAnglersArtStacks(MakeshiftBaitStacks);

        return hasStacks && available;
    }

    protected override DrawOptionsDelegate DrawOptions => () =>
    {
        var stack = MakeshiftBaitStacks;
        if (DrawUtil.EditNumberField(UIStrings.TabAutoCasts_When_Stack_Equals, ref stack))
        {
            // value has to be between 5 and 10
            MakeshiftBaitStacks = Math.Max(5, Math.Min(stack, 10));
            Service.Save();
        }

        ConditionSet = ConditionUi.DrawConditionSetSlim(
            UIStrings.Conditions,
            ConditionSet,
            ConditionScope.AutoCast,
            showAdvanced: true,
            showSubPrefix: true);
    };

    public override int Priority { get; set; } = 9;
    public override bool IsExcludedPriority { get; set; } = false;
}
