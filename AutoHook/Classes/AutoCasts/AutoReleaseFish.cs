namespace AutoHook.Classes.AutoCasts;

public sealed class AutoReleaseFish : BaseActionCast {
    public AutoReleaseFish() : base(UIStrings.ReleaseAllFish, IDs.Actions.Release) => HelpText = UIStrings.ReleaseAllFishHelpText;

    public override int Priority { get; set; } = 14;
    public override bool IsExcludedPriority { get; set; } = false;

    public override bool CastCondition() => true;
}
