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

        var allowedToUseThaliaks = true;
        var hasStacks = Service.WorldState.HasAnglersArtStacks(ThaliaksFavorStacks);

        var notOvercaped = (Service.WorldState.CurrentGp + ThaliaksFavorRecover) < Service.WorldState.MaxGp;

        return hasStacks && notOvercaped && allowedToUseThaliaks; // dont use if its going to overcap gp
    }

    protected override DrawOptionsDelegate DrawOptions => () => {
        var stack = ThaliaksFavorStacks;
        if (DrawUtil.EditNumberField(UIStrings.TabAutoCasts_DrawExtraOptionsThaliaksFavor_, ref stack)) {
            // value has to be between 3 and 10
            ThaliaksFavorStacks = Math.Max(3, Math.Min(stack, 10));
            Service.Save();
        }
        ConditionSet = ConditionUi.DrawConditionSetSlim(
            UIStrings.Conditions,
            ConditionSet,
            ConditionScope.AutoCast,
            showAdvanced: true,
            showSubPrefix: true);
    };

    public override int Priority { get; set; } = 16;
    public override bool IsExcludedPriority { get; set; } = false;
}
