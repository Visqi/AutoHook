using FFXIVClientStructs.FFXIV.Client.Game;

namespace AutoHook.Classes.AutoCasts;

public class AutoSnagging : BaseActionCast {
    public override int Priority { get; set; } = 3;
    public override bool IsExcludedPriority { get; set; } = true;

    public AutoSnagging() : base(UIStrings.Snagging, IDs.Actions.Snagging, ActionType.EventAction) => HelpText = UIStrings.SnaggingHelpText;

    public override string GetName() => Name = UIStrings.Snagging;

    public override bool CastCondition() => !Service.WorldState.HasStatus(IDs.Status.Snagging);
}
