using FFXIVClientStructs.FFXIV.Client.Game;

namespace AutoHook.Classes.AutoCasts;

public sealed class AutoMultiHook : BaseActionCast {
    public AutoMultiHook() : base(IDs.Actions.MultiHook, ActionType.Action) { }

    public override string GetName() => UIStrings.Multihook;

    public override int Priority { get; set; } = 0;
    public override bool IsExcludedPriority { get; set; } = true;

    public override unsafe bool CastCondition() {
        if (!EvaluateConditionSet())
            return false;
        if (Service.WorldState.HasStatus(IDs.Status.Multihook))
            return false;

        if (DutyActionManager.GetInstanceIfReady() is not null and var dm)
            for (var i = 0; i < dm->NumValidSlots; i++)
                if (dm->ActionId[i] is IDs.Actions.MultiHook && dm->CurCharges[i] > 0)
                    return true;
        return false;
    }

    protected override DrawOptionsDelegate DrawOptions => () => DrawAutoCastConditions();
}
