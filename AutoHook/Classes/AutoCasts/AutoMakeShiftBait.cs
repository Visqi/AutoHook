using AutoHook.Ui;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace AutoHook.Classes.AutoCasts;

public sealed class AutoMakeShiftBait : BaseActionCast {
    public int MakeshiftBaitStacks = 5;

    public override bool RequiresTimeWindow() => true;

    public AutoMakeShiftBait() : base(UIStrings.MakeShift_Bait, IDs.Actions.MakeshiftBait, ActionType.Action)
        => HelpText = UIStrings.TabAutoCasts_DrawMakeShiftBait_HelpText;

    public override bool CastCondition() {
        if (!EvaluateConditionSet())
            return false;

        if (Service.WorldState.BlocksFortune())
            return false;

        var available = Service.WorldState.ActionAvailable(IDs.Actions.MakeshiftBait);
        var hasStacks = Service.WorldState.HasAnglersArtStacks(MakeshiftBaitStacks);

        return hasStacks && available;
    }

    protected override DrawOptionsDelegate DrawOptions => () => {
        var stack = MakeshiftBaitStacks;
        if (DrawUtil.EditNumberField(UIStrings.TabAutoCasts_When_Stack_Equals, ref stack)) {
            MakeshiftBaitStacks = Math.Max(5, Math.Min(stack, 10));
            Service.Save();
        }

        DrawAutoCastConditions();
    };

    public override int Priority { get; set; } = 9;
    public override bool IsExcludedPriority { get; set; } = false;
}
