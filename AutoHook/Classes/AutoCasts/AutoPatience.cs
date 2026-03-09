using AutoHook.Ui;
using Dalamud.Bindings.ImGui;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace AutoHook.Classes.AutoCasts;

public class AutoPatience : BaseActionCast {
    public int RefreshEarlyTime = 0;

    public override bool RequiresTimeWindow() => true;

    public override bool DoesCancelMooch() => true;

    public AutoPatience() : base(UIStrings.AutoPatience_Patience, IDs.Actions.Patience2, ActionType.Action) => HelpText = UIStrings.CancelsCurrentMooch;

    public override string GetName()
        => Name = UIStrings.AutoPatience_Patience;

    public override bool CastCondition() {
        if (!EvaluateConditionSet())
            return false;

        if (Service.WorldState.HasStatus(IDs.Status.AnglersFortune) && Service.WorldState.GetStatusTime(IDs.Status.AnglersFortune) > RefreshEarlyTime)
            return false;

        return !Service.WorldState.HasStatus(IDs.Status.PrizeCatch);
    }

    protected override DrawOptionsDelegate DrawOptions => () => {
        if (ImGui.RadioButton(UIStrings.Patience_I, Id == IDs.Actions.Patience)) {
            Id = IDs.Actions.Patience;
            Service.Save();
        }

        if (ImGui.RadioButton(UIStrings.Patience_II, Id == IDs.Actions.Patience2)) {
            Id = IDs.Actions.Patience2;
            Service.Save();
        }

        var time = RefreshEarlyTime;
        if (DrawUtil.EditNumberField(UIStrings.RefreshWhenTimeIsLessThanOrEqual, ref time)) {
            RefreshEarlyTime = Math.Max(0, Math.Min(time, 999));
            Service.Save();
        }

        ConditionSet = ConditionUi.DrawConditionSetSlim(
            UIStrings.Conditions,
            ConditionSet,
            ConditionScope.AutoCast,
            showAdvanced: true,
            showSubPrefix: true);
    };

    public override int Priority { get; set; } = 12;
    public override bool IsExcludedPriority { get; set; } = false;
}
