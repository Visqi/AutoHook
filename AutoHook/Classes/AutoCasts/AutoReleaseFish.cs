namespace AutoHook.Classes.AutoCasts;

public sealed class AutoReleaseFish : BaseActionCast {
    public AutoReleaseFish() : base(IDs.Actions.Release) { }

    public override string GetName() => UIStrings.ReleaseAllFish;

    public override string GetHelpText() => UIStrings.ReleaseAllFishHelpText;

    public override int Priority { get; set; } = 14;
    public override bool IsExcludedPriority { get; set; } = false;

    public override bool CastCondition() => true;
}
