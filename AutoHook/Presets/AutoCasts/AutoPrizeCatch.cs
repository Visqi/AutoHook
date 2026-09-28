using FFXIVClientStructs.FFXIV.Client.Game;
using System.ComponentModel;

namespace AutoHook.Presets.AutoCasts;

public sealed class AutoPrizeCatch : BaseActionCast {
    public override bool DoesCancelMooch() => true;

    public AutoPrizeCatch() : base(IDs.Actions.PrizeCatch, ActionType.Action) { }

    public override string GetName() => UIStrings.Prize_Catch;

    public override string GetHelpText() => UIStrings.Use_Prize_Catch_HelpText;

    public override bool CastCondition(WorldState ws) {
        if (!EvaluateConditionSet(ws))
            return false;

        if (!Enabled)
            return false;

        if (ws.BlocksFortune())
            return false;

        return ws.ActionAvailable(IDs.Actions.PrizeCatch);
    }

    protected override DrawOptionsDelegate DrawOptions => () => DrawAutoCastConditions();

    [DefaultValue(13)]
    public override int Priority { get; set; } = 13;
    public override bool IsExcludedPriority { get; set; } = false;
}
