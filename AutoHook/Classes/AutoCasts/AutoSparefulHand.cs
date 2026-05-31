using FFXIVClientStructs.FFXIV.Client.Game;

namespace AutoHook.Classes.AutoCasts;

public class AutoSparefulHand : BaseActionCast {
    public AutoSparefulHand() : base(UIStrings.SparefulHand, IDs.Actions.SparefulHand, ActionType.Action) => HelpText = UIStrings.SparefulHand_HelpText;

    public override string GetName()
        => Name = UIStrings.SparefulHand;

    public uint? FishIdToCheck { get; set; }

    public override bool CastCondition() {
        var ws = Service.WorldState;
        if (FishIdToCheck is { } fishId)
            ws.SwimbaitEvaluationFishId = fishId;
        try {
            return EvaluateConditionSet();
        }
        finally {
            ws.SwimbaitEvaluationFishId = 0;
        }
    }

    protected override DrawOptionsDelegate? DrawOptions => () => {
        DrawAutoCastConditions(showSubPrefix: false);
    };

    public override int Priority { get; set; } = 20;
    public override bool IsExcludedPriority { get; set; } = false;
}
