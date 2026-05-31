using AutoHook.Ui;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace AutoHook.Classes.AutoCasts;

public sealed class AutoPrizeCatch : BaseActionCast {
    public override bool DoesCancelMooch() => true;

    public AutoPrizeCatch() : base(UIStrings.Prize_Catch, IDs.Actions.PrizeCatch, ActionType.Action) {
        HelpText = UIStrings.Use_Prize_Catch_HelpText;
    }

    public override bool CastCondition() {
        if (!EvaluateConditionSet())
            return false;

        if (!Enabled)
            return false;

        if (Service.WorldState.BlocksFortune())
            return false;

        return Service.WorldState.ActionAvailable(IDs.Actions.PrizeCatch);
    }

    protected override DrawOptionsDelegate DrawOptions => () => DrawAutoCastConditions();

    public override int Priority { get; set; } = 13;
    public override bool IsExcludedPriority { get; set; } = false;
}
