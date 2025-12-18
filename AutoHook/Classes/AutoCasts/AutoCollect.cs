using FFXIVClientStructs.FFXIV.Client.Game;

namespace AutoHook.Classes.AutoCasts;

public class AutoCollect : BaseActionCast
{
    public override int Priority { get; set; } = 2;
    public override bool IsExcludedPriority { get; set; } = true;

    public AutoCollect() : base(UIStrings.Collect, IDs.Actions.Collect, ActionType.EventAction)
    {
        HelpText = UIStrings.CollectHelpText;
    }

    public override string GetName()
        => Name = UIStrings.Collect;

    public override bool CastCondition() => !PlayerRes.HasStatus(IDs.Status.CollectorsGlove);

    //protected override DrawOptionsDelegate DrawOptions => () =>
    //{

    //};
}
