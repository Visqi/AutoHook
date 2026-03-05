using AutoHook.Conditions;
using AutoHook.Ui;

namespace AutoHook.Classes.AutoCasts;

public class AutoChum : BaseActionCast
{
    [Obsolete("Legacy config. Replaced by ConditionSet.")] public bool _onlyUseWithIntuition;
    [Obsolete("Legacy config. Replaced by ConditionSet.")] public int _useWhenIntuitionExceeds = 0;

    public ConditionSet? ConditionSet { get; set; }

    public override bool DoesCancelMooch() => true;

    public AutoChum() : base(UIStrings.Chum, IDs.Actions.Chum) => HelpText = UIStrings.CancelsCurrentMooch;

    public override string GetName() => Name = UIStrings.Chum;

    public override bool CastCondition() => ConditionSet is not { Groups.Count: > 0 } || ConditionSet.Evaluate(Service.WorldState, Conditions.Conditions.Registry);

    protected override DrawOptionsDelegate DrawOptions => () =>
    {
        ConditionSet = ConditionUi.DrawConditionSet(UIStrings.Conditions, ConditionSet, ConditionScope.AutoCast, showPresets: true);
    };

    public override int Priority { get; set; } = 1;
    public override bool IsExcludedPriority { get; set; } = false;
}
