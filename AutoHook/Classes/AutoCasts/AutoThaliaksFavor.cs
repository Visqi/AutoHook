using FFXIVClientStructs.FFXIV.Client.Game;

namespace AutoHook.Classes.AutoCasts;

public sealed class AutoThaliaksFavor : BaseActionCast {
    public int ThaliaksFavorStacks = 3;
    public int ThaliaksFavorRecover = 150;

    public AutoThaliaksFavor(bool isSpearfishing = false) : base(UIStrings.Thaliaks_Favor, IDs.Actions.ThaliaksFavor, ActionType.Action) {
        HelpText = UIStrings.TabAutoCasts_DrawThaliaksFavor_HelpText;
        IsSpearFishing = isSpearfishing;
    }

    public override bool CastCondition() {
        if (!EvaluateConditionSet())
            return false;

        var hasStacks = Service.WorldState.GetStatusStacks(IDs.Status.AnglersArt) >= ThaliaksFavorStacks;
        var notOvercaped = Service.WorldState.Player.CurrentGp + ThaliaksFavorRecover < Service.WorldState.Player.MaxGp;

        return hasStacks && notOvercaped;
    }

    protected override DrawOptionsDelegate DrawOptions => () => {
        var stack = ThaliaksFavorStacks;
        if (DrawUtil.EditNumberField(UIStrings.TabAutoCasts_DrawExtraOptionsThaliaksFavor_, ref stack)) {
            ThaliaksFavorStacks = Math.Max(3, Math.Min(stack, 10));
            Service.Save();
        }
        DrawAutoCastConditions();
    };

    public override int Priority { get; set; } = 16;
    public override bool IsExcludedPriority { get; set; } = false;
}
