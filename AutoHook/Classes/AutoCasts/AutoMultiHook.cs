using AutoHook.Conditions;
using AutoHook.Ui;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace AutoHook.Classes.AutoCasts;

public class AutoMultiHook : BaseActionCast
{
    [Obsolete("Legacy config. Replaced by ConditionSet.")] public bool OnlyUseWhenIdenticalCastActive;

    public ConditionSet? ConditionSet { get; set; }

    public AutoMultiHook() : base(UIStrings.Multihook, IDs.Actions.MultiHook) { }

    public override int Priority { get; set; } = 0;
    public override bool IsExcludedPriority { get; set; } = true;
    public override unsafe bool CastCondition()
    {
        if (ConditionSet is { Groups.Count: > 0 } &&
            !ConditionSet.Evaluate(Service.WorldState, Conditions.Conditions.Registry))
            return false;

        if (DutyActionManager.GetInstanceIfReady() is not null and var dm)
            for (var i = 0; i < dm->NumValidSlots; i++)
                if (dm->ActionId[i] is IDs.Actions.MultiHook && dm->CurCharges[i] > 0)
                    return true;
        return false;
    }

    public override string GetName() => Name = UIStrings.Multihook;

    protected override DrawOptionsDelegate DrawOptions => () =>
    {
        ConditionSet = ConditionUi.DrawConditionSet(UIStrings.Conditions, ConditionSet, ConditionScope.AutoCast, showPresets: true);
    };
}
