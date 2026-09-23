using System.ComponentModel;

namespace AutoHook.Classes.AutoCasts;

public sealed class AutoVitalSight : BaseActionCast {
    [DefaultValue(2)]
    public int RequiredStacks = 2;

    public AutoVitalSight(bool isSpearfishing = true) : base(IDs.Actions.VitalSight) {
        IsSpearFishing = isSpearfishing;
        GpThreshold = 0;
    }

    public override string GetName() => UIStrings.VitalSight;

    public override bool ShowGpThreshold => false;

    public override bool CastCondition() {
        if (!EvaluateConditionSet())
            return false;

        if (Service.WorldState.Player.HasStatus(IDs.Status.VitalSight))
            return false;

        return Service.WorldState.Player.GetStatusStacks(IDs.Status.AnglersArt) >= RequiredStacks;
    }

    protected override DrawOptionsDelegate DrawOptions => () => {
        var stack = RequiredStacks;
        if (DrawUtil.EditNumberField(UIStrings.TabAutoCasts_DrawExtraOptionsThaliaksFavor_, ref stack)) {
            RequiredStacks = Math.Max(2, Math.Min(stack, 10));
            Service.Save();
        }
        DrawAutoCastConditions();
    };

    [DefaultValue(true)]
    public override bool IsExcludedPriority { get; set; } = true;
}
