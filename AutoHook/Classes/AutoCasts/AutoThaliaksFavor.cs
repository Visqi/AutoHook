using AutoHook.Ui;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace AutoHook.Classes.AutoCasts;

public class AutoThaliaksFavor : BaseActionCast {
    public int ThaliaksFavorStacks = 3;
    public int ThaliaksFavorRecover = 150;

    public AutoThaliaksFavor(bool isSpearfishing = false) : base(UIStrings.Thaliaks_Favor, IDs.Actions.ThaliaksFavor, ActionType.Action) {
        HelpText = UIStrings.TabAutoCasts_DrawThaliaksFavor_HelpText;
        IsSpearFishing = isSpearfishing;
    }

    public override string GetName()
        => Name = UIStrings.Thaliaks_Favor;

    public override bool CastCondition() {
        if (!EvaluateConditionSet())
            return false;

        var currentStacks = Service.WorldState.GetStatusStacks(IDs.Status.AnglersArt);
        var hasStacks = currentStacks >= ThaliaksFavorStacks;

        var projectedGp = Service.WorldState.CurrentGp + ThaliaksFavorRecover;
        var notOvercaped = projectedGp < Service.WorldState.MaxGp;

        return hasStacks && notOvercaped; // dont use if its going to overcap gp
    }

    protected override DrawOptionsDelegate DrawOptions => () => {
        var stack = ThaliaksFavorStacks;
        if (DrawUtil.EditNumberField(UIStrings.TabAutoCasts_DrawExtraOptionsThaliaksFavor_, ref stack)) {
            // value has to be between 3 and 10
            ThaliaksFavorStacks = Math.Max(3, Math.Min(stack, 10));
            Service.Save();
        }
        DrawAutoCastConditions();
    };

    public override int Priority { get; set; } = 16;
    public override bool IsExcludedPriority { get; set; } = false;
}
