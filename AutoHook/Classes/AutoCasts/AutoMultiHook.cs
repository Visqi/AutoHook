using FFXIVClientStructs.FFXIV.Client.Game;

namespace AutoHook.Classes.AutoCasts;
public class AutoMultiHook : BaseActionCast {
    public AutoMultiHook() : base(UIStrings.Multihook, IDs.Actions.MultiHook, ActionType.EventAction) { }

    public override int Priority { get; set; } = 0;
    public override bool IsExcludedPriority { get; set; } = true;
    public override unsafe bool CastCondition() {
        if (!EvaluateConditionSet())
            return false;

        if (DutyActionManager.GetInstanceIfReady() is not null and var dm)
            for (var i = 0; i < dm->NumValidSlots; i++)
                if (dm->ActionId[i] is IDs.Actions.MultiHook && dm->CurCharges[i] > 0)
                    return true;
        return false;
    }

    public override string GetName() => Name = UIStrings.Multihook;

    protected override DrawOptionsDelegate DrawOptions => () => {
        DrawAutoCastConditions();
    };
}
