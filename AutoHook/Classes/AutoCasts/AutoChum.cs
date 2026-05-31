using FFXIVClientStructs.FFXIV.Client.Game;

namespace AutoHook.Classes.AutoCasts;

public class AutoChum : BaseActionCast {
    public override bool DoesCancelMooch() => true;

    public AutoChum() : base(UIStrings.Chum, IDs.Actions.Chum) => HelpText = UIStrings.CancelsCurrentMooch;

    public override string GetName() => Name = UIStrings.Chum;

    public override bool CastCondition() => EvaluateConditionSet();

    protected override DrawOptionsDelegate DrawOptions => () => DrawAutoCastConditions();

    public override int Priority { get; set; } = 1;
    public override bool IsExcludedPriority { get; set; } = false;
}
