using AutoHook.Spearfishing;

namespace AutoHook.Modules.Gig;

public sealed class GigAutoCastComponent(GigFishingModule module) : GigFishingComponent(module) {
    private static SpearFishingPresets GigCfg => Configuration.C.AutoGigConfig;

    public override void ContributeHints(ActionHints hints) {
        var selectedPreset = GigCfg.SelectedPreset;

        Propose(hints, selectedPreset is { Collect.Enabled: true } ? selectedPreset.Collect : GigCfg.Collect);
        Propose(hints, GigCfg.NatureBountyBeforeFishAction);
        Propose(hints, selectedPreset is { BaitedBreath.Enabled: true } ? selectedPreset.BaitedBreath : GigCfg.BaitedBreath);
        Propose(hints, selectedPreset is { VitalSight.Enabled: true } ? selectedPreset.VitalSight : GigCfg.VitalSight);
        Propose(hints, selectedPreset is { ElectricCurrent.Enabled: true } ? selectedPreset.ElectricCurrent : GigCfg.ElectricCurrent);
        Propose(hints, selectedPreset is { ThaliaksFavor.Enabled: true } ? selectedPreset.ThaliaksFavor : GigCfg.ThaliaksFavor);
        Propose(hints, selectedPreset is { Cordial.Enabled: true } ? selectedPreset.Cordial : GigCfg.Cordial);
    }

    public void ContributeVeteranTradeHints(ActionHints hints, AutoVeteranTrade? veteranTrade) {
        if (veteranTrade?.IsAvailableToCast(Ws) != true)
            return;

        hints.AddCast(HintPriority.ForGigAutoCast(veteranTrade), new ActionRequest(veteranTrade.Id, veteranTrade.ActionType, veteranTrade.GetName()), HintSource.GigAutoCast, context: DecisionContext.AutoCast);
    }

    private void Propose(ActionHints hints, BaseActionCast action) {
        if (!action.IsAvailableToCast(Ws))
            return;

        hints.AddCast(HintPriority.ForGigAutoCast(action), new ActionRequest(action.Id, action.ActionType, action.GetName()), HintSource.GigAutoCast, context: DecisionContext.AutoCast);
    }
}
