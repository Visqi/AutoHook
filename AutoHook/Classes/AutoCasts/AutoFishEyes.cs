using FFXIVClientStructs.FFXIV.Client.Game;

namespace AutoHook.Classes.AutoCasts;

public class AutoFishEyes : BaseActionCast
{
    public override int Priority { get; set; } = 6;
    public override bool IsExcludedPriority { get; set; } = false;

    public bool OnlyWhenMakeShiftUp;

    public bool IgnoreMooch;

    public override bool DoesCancelMooch() => !IgnoreMooch;

    public override bool RequiresTimeWindow() => true;

    public AutoFishEyes() : base(UIStrings.Fish_Eyes, IDs.Actions.FishEyes, ActionType.Action)
    {
        HelpText = UIStrings.CancelsCurrentMooch;
    }

    public override string GetName()
        => Name = UIStrings.Fish_Eyes;

    public override bool CastCondition()
    {
        if (PlayerRes.HasStatus(IDs.Status.FishEyes))
            return false;

        if (OnlyWhenMakeShiftUp && !PlayerRes.HasStatus(IDs.Status.MakeshiftBait) &&
            !PlayerRes.HasStatus(IDs.Status.AnglersFortune))
            return false;

        return true;
    }

    protected override DrawOptionsDelegate DrawOptions => () =>
    {
        DrawUtil.Checkbox(UIStrings.OnlyWhenMakeshiftOrPatience, ref OnlyWhenMakeShiftUp);

        DrawUtil.Checkbox(UIStrings.IgnoreMooch, ref IgnoreMooch, UIStrings.IgnoreMoochFishEyes);
    };
}