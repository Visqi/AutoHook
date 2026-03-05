using AutoHook.Conditions;
using AutoHook.Ui;
using FFXIVClientStructs.FFXIV.Client.Game;
using Dalamud.Bindings.ImGui;

namespace AutoHook.Classes.AutoCasts;

public class AutoIdenticalCast : BaseActionCast
{
    [Obsolete("Legacy config. Replaced by ConditionSet.")] public bool OnlyUseUnderPatience;
    [Obsolete("Legacy config. Replaced by ConditionSet.")] public bool OnlyWhenCordialAvailable;

    public bool OnlyUseAfterXAmount;
    public int CaughtAmountLimit = 1;

    public ConditionSet? ConditionSet { get; set; }

    public override bool DoesCancelMooch() => true;

    public AutoIdenticalCast() : base(UIStrings.Identical_Cast, IDs.Actions.IdenticalCast, ActionType.Action)
        => HelpText = UIStrings.OverridesSurfaceSlap;

    public override string GetName()
        => Name = UIStrings.Identical_Cast;

    public override bool CastCondition()
    {
        if (ConditionSet is { Groups.Count: > 0 } &&
            !ConditionSet.Evaluate(Service.WorldState, Conditions.Conditions.Registry))
            return false;

        return !Service.WorldState.HasStatus(IDs.Status.IdenticalCast) && !Service.WorldState.HasStatus(IDs.Status.SurfaceSlap);
    }

    public bool IsAvailableToCast(int caughtAmount) => (!OnlyUseAfterXAmount || caughtAmount >= CaughtAmountLimit) && IsAvailableToCast();

    protected override DrawOptionsDelegate DrawOptions => () =>
    {
        var stack = CaughtAmountLimit;

        if (DrawUtil.Checkbox(UIStrings.Only_use_when_the_fish_is_caught, ref OnlyUseAfterXAmount))
            Service.Save();

        ImGui.SameLine();

        ImGui.SetNextItemWidth(30);
        if (ImGui.InputInt(UIStrings.TimeS, ref stack, 0, 0))
        {
            CaughtAmountLimit = Math.Max(1, Math.Min(stack, 999));
            Service.Save();
        }

        if (DrawUtil.Checkbox(UIStrings.Dont_Cancel_Mooch, ref DontCancelMooch, UIStrings.IdenticalCast_HelpText, true))
            Service.Save();
        ConditionSet = ConditionUi.DrawConditionSet(UIStrings.Conditions, ConditionSet, ConditionScope.AutoCast, showPresets: true);
    };

    public override int Priority { get; set; } = 8;
    public override bool IsExcludedPriority { get; set; } = false;
}
