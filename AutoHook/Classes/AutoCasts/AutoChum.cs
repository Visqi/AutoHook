using AutoHook.Ui;

namespace AutoHook.Classes.AutoCasts;

public class AutoChum : BaseActionCast
{
    [Obsolete("Legacy config. Replaced by ConditionSet.")] public bool _onlyUseWithIntuition;
    [Obsolete("Legacy config. Replaced by ConditionSet.")] public int _useWhenIntuitionExceeds = 0;

    public override bool DoesCancelMooch() => true;

    public AutoChum() : base(UIStrings.Chum, IDs.Actions.Chum) => HelpText = UIStrings.CancelsCurrentMooch;

    public override string GetName() => Name = UIStrings.Chum;

    public override bool CastCondition() => EvaluateConditionSet();

    protected override DrawOptionsDelegate DrawOptions => () =>
    {
        ConditionSet = ConditionUi.DrawConditionSetSlim(
            UIStrings.Conditions,
            ConditionSet,
            ConditionScope.AutoCast,
            showAdvanced: true,
            showSubPrefix: true);
    };

    public override int Priority { get; set; } = 1;
    public override bool IsExcludedPriority { get; set; } = false;
}
