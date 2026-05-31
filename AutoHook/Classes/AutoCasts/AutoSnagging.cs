using FFXIVClientStructs.FFXIV.Client.Game;

namespace AutoHook.Classes.AutoCasts;

public sealed class AutoSnagging : BaseActionCast {
    public override int Priority { get; set; } = 3;
    public override bool IsExcludedPriority { get; set; } = true;

    public AutoSnagging() : base(UIStrings.Snagging, IDs.Actions.Snagging, ActionType.Action) => HelpText = UIStrings.SnaggingHelpText;

    public override bool CastCondition() => !Service.WorldState.HasStatus(IDs.Status.Snagging);
}
