using AutoHook.Conditions;
using AutoHook.Ui;

namespace AutoHook.Classes.AutoCasts;

public class AutoBigGameFishing : BaseActionCast
{
    public int AnglersStacks = 2;

    [Obsolete("Legacy config. Replaced by ConditionSet.")] public bool WithIdenticalC = false;
    [Obsolete("Legacy config. Replaced by ConditionSet.")] public bool WithSlap = false;

    public ConditionSet? ConditionSet { get; set; }

    public AutoBigGameFishing() : base(UIStrings.BigGameFishing, IDs.Actions.BigGameFishing) { }

    public override string GetName()
        => Name = UIStrings.BigGameFishing;

    public override bool CastCondition()
    {
        if (ConditionSet is { Groups.Count: > 0 } &&
            !ConditionSet.Evaluate(Service.WorldState, Conditions.Conditions.Registry))
            return false;

        if (Service.WorldState.HasStatus(IDs.Status.BigGameFishing))
            return false;

        var hasStacks = Service.WorldState.HasAnglersArtStacks(AnglersStacks);

        return hasStacks;
    }

    protected override DrawOptionsDelegate DrawOptions => () =>
    {
        var stack = AnglersStacks;
        if (DrawUtil.EditNumberField(UIStrings.TabAutoCasts_DrawExtraOptionsThaliaksFavor_, ref stack, "", 1))
        {
            AnglersStacks = Math.Max(2, Math.Min(stack, 10));
            Service.Save();
        }

        ConditionSet = ConditionUi.DrawConditionSet(UIStrings.Conditions, ConditionSet, ConditionScope.AutoCast, showPresets: true);
    };

    public override int Priority { get; set; } = 18;
    public override bool IsExcludedPriority { get; set; } = false;
}
