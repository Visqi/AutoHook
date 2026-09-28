using FFXIVClientStructs.FFXIV.Client.Game;
using System.ComponentModel;

namespace AutoHook.Presets.AutoCasts;

public sealed class AutoFishEyes : BaseActionCast {
    [DefaultValue(6)]
    public override int Priority { get; set; } = 6;
    public override bool IsExcludedPriority { get; set; } = false;

    public override bool DoesCancelMooch() => true;

    public override bool RequiresTimeWindow() => true;

    public AutoFishEyes() : base(IDs.Actions.FishEyes, ActionType.Action) { }

    public override string GetName() => UIStrings.Fish_Eyes;

    public override string GetHelpText() => UIStrings.CancelsCurrentMooch;

    public override bool CastCondition(WorldState ws) => EvaluateConditionSet(ws) && !ws.Player.HasStatus(IDs.Status.FishEyes);

    protected override DrawOptionsDelegate DrawOptions => () => {
        DrawAutoCastConditions();
    };
}
