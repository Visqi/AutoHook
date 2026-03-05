using AutoHook.Ui;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace AutoHook.Classes.AutoCasts;

public class AutoMultiHook : BaseActionCast
{
    [Obsolete("Legacy config. Replaced by ConditionSet.")] public bool OnlyUseWhenIdenticalCastActive;

    public AutoMultiHook() : base(UIStrings.Multihook, IDs.Actions.MultiHook) { }

    public override int Priority { get; set; } = 0;
    public override bool IsExcludedPriority { get; set; } = true;
    public override unsafe bool CastCondition()
    {
        if (!EvaluateConditionSet())
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
