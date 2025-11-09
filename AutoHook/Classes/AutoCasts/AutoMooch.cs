using FFXIVClientStructs.FFXIV.Client.Game;

namespace AutoHook.Classes.AutoCasts;

public class AutoMooch : BaseActionCast
{
    public AutoMooch2 Mooch2 = new();

    public bool OnlyMoochIntuition = false;

    public override bool RequiresTimeWindow() => true;

    public AutoMooch() : base(UIStrings.AutoMooch, IDs.Actions.Mooch, ActionType.Action)
    {
        HelpText = UIStrings.AutoMooch_HelpText;
    }

    public override string GetName()
        => Name = UIStrings.AutoMooch;

    public override bool CastCondition()
    {
        if (OnlyMoochIntuition && !PlayerRes.HasStatus(IDs.Status.FishersIntuition))
            return false;

        if (Mooch2.IsAvailableToCast())
        {
            Service.PrintDebug(@$"Mooch2 Available, casting mooch2");
            Id = IDs.Actions.Mooch2;
            return true;
        }

        if (PlayerRes.ActionTypeAvailable(IDs.Actions.Mooch))
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
        if (DrawUtil.Checkbox(UIStrings.TabAutoCasts_DrawExtraOptionsAutoMooch_Extra_Only_Active,
                ref OnlyMoochIntuition))
        {
            Service.Save();
        }
    };

    public override int Priority { get; set; } = 10;
    public override bool IsExcludedPriority { get; set; } = true;
}