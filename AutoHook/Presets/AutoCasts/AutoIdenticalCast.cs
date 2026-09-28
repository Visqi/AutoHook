using FFXIVClientStructs.FFXIV.Client.Game;
using System.ComponentModel;

namespace AutoHook.Presets.AutoCasts;

public sealed class AutoIdenticalCast : BaseActionCast {
    public AutoIdenticalCast() : base(IDs.Actions.IdenticalCast, ActionType.Action) { }

    public override string GetName() => UIStrings.Identical_Cast;

    public override string GetHelpText() => UIStrings.OverridesSurfaceSlap;

    public override bool CastCondition(WorldState ws) => EvaluateConditionSet(ws)
        && !ws.Player.HasStatus(IDs.Status.IdenticalCast)
        && !ws.Player.HasStatus(IDs.Status.SurfaceSlap);

    protected override DrawOptionsDelegate DrawOptions => () => DrawAutoCastConditions();

    [DefaultValue(8)]
    public override int Priority { get; set; } = 8;
    public override bool IsExcludedPriority { get; set; } = false;
}
