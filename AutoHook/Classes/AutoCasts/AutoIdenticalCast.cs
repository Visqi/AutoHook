using AutoHook.Ui;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace AutoHook.Classes.AutoCasts;

public sealed class AutoIdenticalCast : BaseActionCast {
    public bool OnlyUseAfterXAmount;
    public int CaughtAmountLimit = 1;

    public override bool DoesCancelMooch() => true;

    public AutoIdenticalCast() : base(UIStrings.Identical_Cast, IDs.Actions.IdenticalCast, ActionType.Action)
        => HelpText = UIStrings.OverridesSurfaceSlap;

    public override bool CastCondition() => EvaluateConditionSet()
        && !Service.WorldState.HasStatus(IDs.Status.IdenticalCast)
        && !Service.WorldState.HasStatus(IDs.Status.SurfaceSlap);

    public bool IsAvailableToCast(int caughtAmount) => (!OnlyUseAfterXAmount || caughtAmount >= CaughtAmountLimit) && IsAvailableToCast();

    public void DrawFishTabOptions() {
        DrawFishCaughtActionOptions();

        var stack = CaughtAmountLimit;

        DrawUtil.Checkbox(UIStrings.Only_use_when_the_fish_is_caught, ref OnlyUseAfterXAmount);

        ImGui.SameLine();

        ImGui.SetNextItemWidth(30.Scaled());
        if (ImGui.InputInt(UIStrings.TimeS, ref stack, 0, 0)) {
            CaughtAmountLimit = Math.Max(1, Math.Min(stack, 999));
            Service.Save();
        }
    }

    protected override DrawOptionsDelegate DrawOptions => () => {
        DrawAutoCastConditions();

        var stack = CaughtAmountLimit;

        DrawUtil.Checkbox(UIStrings.Only_use_when_the_fish_is_caught, ref OnlyUseAfterXAmount);

        ImGui.SameLine();

        ImGui.SetNextItemWidth(30.Scaled());
        if (ImGui.InputInt(UIStrings.TimeS, ref stack, 0, 0)) {
            CaughtAmountLimit = Math.Max(1, Math.Min(stack, 999));
            Service.Save();
        }

        DrawUtil.Checkbox(UIStrings.Dont_Cancel_Mooch, ref DontCancelMooch, UIStrings.IdenticalCast_HelpText, true);
    };

    public override int Priority { get; set; } = 8;
    public override bool IsExcludedPriority { get; set; } = false;
}
