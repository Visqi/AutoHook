using FFXIVClientStructs.FFXIV.Client.Game;

namespace AutoHook.Classes.AutoCasts;

public class AutoFood : BaseActionCast //todo
{
    public float SecondsRemaining = 0;

    public override int Priority { get; set; } = 7;
    public override bool IsExcludedPriority { get; set; } = false;

    public AutoFood() : base(UIStrings.Food_Buff, 0, ActionType.Item) { }

    public override string GetName()
        => Name = UIStrings.Food_Buff;

    public override bool CastCondition() => Service.WorldState.GetStatusTime(IDs.Status.FoodBuff) <= SecondsRemaining;

    /*protected override DrawOptionsDelegate DrawOptions => () =>
    {

    };*/
}
