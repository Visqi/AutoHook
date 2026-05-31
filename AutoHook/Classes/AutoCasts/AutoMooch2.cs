using FFXIVClientStructs.FFXIV.Client.Game;

namespace AutoHook.Classes.AutoCasts;

public sealed class AutoMooch2 : BaseActionCast {
    public override int Priority { get; set; } = 11;
    public override bool IsExcludedPriority { get; set; } = true;

    public AutoMooch2() : base(UIStrings.Mooch_II, IDs.Actions.Mooch2, ActionType.Action) => HelpText = UIStrings.AutoMooch_HelpText;

    public override bool CastCondition() => true;
}
