using AutoHook.Ui;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace AutoHook.Classes.AutoCasts;

public class AutoPrizeCatch : BaseActionCast
{
    [Obsolete("Legacy config. Replaced by ConditionSet.")] public bool UseWhenMoochIIOnCD = false;
    [Obsolete("Legacy config. Replaced by ConditionSet.")] public bool UseOnlyWithIdenticalCast = false;
    [Obsolete("Legacy config. Replaced by ConditionSet.")] public bool UseOnlyWithActiveSlap = false;

    public override bool DoesCancelMooch() => true;

    public AutoPrizeCatch() : base(UIStrings.Prize_Catch, IDs.Actions.PrizeCatch, ActionType.Action)
    {
        HelpText = UIStrings.Use_Prize_Catch_HelpText;
    }

    public override string GetName()
        => Name = UIStrings.Prize_Catch;

    public override bool CastCondition()
    {
        if (!EvaluateConditionSet())
            return false;

        if (!Enabled)
            return false;

        if (Service.WorldState.HasStatus(IDs.Status.MakeshiftBait))
            return false;

        if (Service.WorldState.HasStatus(IDs.Status.PrizeCatch))
            return false;

        if (Service.WorldState.HasStatus(IDs.Status.AnglersFortune))
            return false;

        return Service.WorldState.ActionAvailable(IDs.Actions.PrizeCatch);
    }

    protected override DrawOptionsDelegate DrawOptions => () =>
    {
        ConditionSet = ConditionUi.DrawConditionSetSlim(
            UIStrings.Conditions,
            ConditionSet,
            ConditionScope.AutoCast,
            showAdvanced: true,
            showSubPrefix: true);
    };

    public override int Priority { get; set; } = 13;
    public override bool IsExcludedPriority { get; set; } = false;
}
