using AutoHook.Ui;
using Dalamud.Bindings.ImGui;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace AutoHook.Classes.AutoCasts;

public class AutoIdenticalCast : BaseActionCast {
    public bool OnlyUseAfterXAmount;
    public int CaughtAmountLimit = 1;

    public override bool DoesCancelMooch() => true;

    public AutoIdenticalCast() : base(UIStrings.Identical_Cast, IDs.Actions.IdenticalCast, ActionType.Action)
        => HelpText = UIStrings.OverridesSurfaceSlap;

    public override string GetName()
        => Name = UIStrings.Identical_Cast;

    public override bool CastCondition() => EvaluateConditionSet() && !Service.WorldState.HasStatus(IDs.Status.IdenticalCast) && !Service.WorldState.HasStatus(IDs.Status.SurfaceSlap);

    public bool IsAvailableToCast(int caughtAmount) => (!OnlyUseAfterXAmount || caughtAmount >= CaughtAmountLimit) && IsAvailableToCast();

    protected override DrawOptionsDelegate DrawOptions => () => {
        ConditionSet = ConditionUi.DrawConditionSetSlim(
            UIStrings.Conditions,
            ConditionSet,
            ConditionScope.AutoCast,
            showAdvanced: true,
            showSubPrefix: true);

        var stack = CaughtAmountLimit;

        if (DrawUtil.Checkbox(UIStrings.Only_use_when_the_fish_is_caught, ref OnlyUseAfterXAmount))
            Service.Save();

        ImGui.SameLine();

        ImGui.SetNextItemWidth(30);
        if (ImGui.InputInt(UIStrings.TimeS, ref stack, 0, 0)) {
            CaughtAmountLimit = Math.Max(1, Math.Min(stack, 999));
            Service.Save();
        }

        if (DrawUtil.Checkbox(UIStrings.Dont_Cancel_Mooch, ref DontCancelMooch, UIStrings.IdenticalCast_HelpText, true))
            Service.Save();
    };

    public override int Priority { get; set; } = 8;
    public override bool IsExcludedPriority { get; set; } = false;
}
