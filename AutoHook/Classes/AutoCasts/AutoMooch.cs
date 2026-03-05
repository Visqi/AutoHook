using AutoHook.Conditions;
using AutoHook.Ui;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace AutoHook.Classes.AutoCasts;

public class AutoMooch : BaseActionCast
{
    public AutoMooch2 Mooch2 = new();

    [Obsolete("Legacy config. Replaced by ConditionSet.")] public bool OnlyMoochIntuition = false;

    public ConditionSet? ConditionSet { get; set; }

    public override bool RequiresTimeWindow() => true;

    public AutoMooch() : base(UIStrings.AutoMooch, IDs.Actions.Mooch, ActionType.Action)
        => HelpText = UIStrings.AutoMooch_HelpText;

    public override string GetName()
        => Name = UIStrings.AutoMooch;

    public override bool CastCondition()
    {
        if (ConditionSet is { Groups.Count: > 0 } &&
            !ConditionSet.Evaluate(Service.WorldState, Conditions.Conditions.Registry))
            return false;

        if (Mooch2.IsAvailableToCast())
        {
            Service.PrintDebug(@$"Mooch2 Available, casting mooch2");
            Id = IDs.Actions.Mooch2;
            return true;
        }

        if (Service.WorldState.ActionAvailable(IDs.Actions.Mooch))
        {
            Service.PrintDebug(@$"Mooch Available, casting normal mooch");
            Id = IDs.Actions.Mooch;
            return true;
        }

        return false;
    }

    protected override DrawOptionsDelegate DrawOptions => () =>
    {
        Mooch2.DrawConfig(null);
        ConditionSet = ConditionUi.DrawConditionSet(UIStrings.Conditions, ConditionSet, ConditionScope.AutoCast, showPresets: true);
    };

    public override int Priority { get; set; } = 10;
    public override bool IsExcludedPriority { get; set; } = true;
}
