using Dalamud.Bindings.ImGui;
using ECommons.Throttlers;
using FFXIVClientStructs.FFXIV.Client.Game.Event;
using AutoHook.Ui;

namespace AutoHook.Classes.AutoCasts;

public class AutoLures : BaseActionCast
{
    public int LureStacks = 3;
    public bool CancelAttempt;

    public LureTarget LureTarget;

    public AutoLures() : base(UIStrings.UseLures, IDs.Actions.AmbitiousLure) { }

    [Obsolete("Legacy config")] public bool OnlyWhenActiveSlap;
    [Obsolete("Legacy config")] public bool OnlyWhenNotActiveSlap;
    [Obsolete("Legacy config")] public bool OnlyWhenActiveIdentical;
    [Obsolete("Legacy config")] public bool OnlyWhenNotActiveIdentical;
    [Obsolete("Legacy config")] public bool OnlyCastLarge;

    public override string GetName()
        => Name = UIStrings.UseLures;

    private uint StatusId => Id == IDs.Actions.AmbitiousLure ? IDs.Status.AmbitiousLure : IDs.Status.ModestLure;

    public override bool CastCondition()
    {
        if (Service.WorldState.GetStatusStacks(StatusId) >= LureStacks)
            return false;

        if (Service.WorldState.FishingState is not (FishingState.AmbitiousLure or FishingState.LineInWater))
            return false;

        return EvaluateConditionSet();
    }

    protected override DrawOptionsDelegate DrawOptions => () =>
    {
        DrawUtil.TextV(UIStrings.LureType);
        ImGui.SameLine();

        if (ImGui.RadioButton(UIStrings.AmbitiousLure, Id == IDs.Actions.AmbitiousLure))
        {
            Id = IDs.Actions.AmbitiousLure;
            Service.Save();
        }

        ImGui.SameLine();

        if (ImGui.RadioButton(UIStrings.ModestLure, Id == IDs.Actions.ModestLure))
        {
            Id = IDs.Actions.ModestLure;
            Service.Save();
        }

        var stack = LureStacks;

        DrawUtil.TextV(UIStrings.AutoLures_Target_Fish);
        ImGui.SameLine();
        if (ImGui.RadioButton(UIStrings.AnyTarget, LureTarget == LureTarget.Any))
        {
            LureTarget = LureTarget.Any;
            Service.Save();
        }

        ImGui.SameLine();
        if (ImGui.RadioButton(UIStrings.OnlySpecial, LureTarget == LureTarget.Special))
        {
            LureTarget = LureTarget.Special;
            Service.Save();
        }

        ImGui.SameLine();
        DrawUtil.Info($"{UIStrings.SpecialFishExemple} {GameRes.LureFishes.FirstOrDefault()?.Name}");

        ImGui.SameLine();
        if (ImGui.RadioButton(UIStrings.NotSpecial, LureTarget == LureTarget.NotSpecial))
        {
            LureTarget = LureTarget.NotSpecial;
            Service.Save();
        }

        if (DrawUtil.EditNumberField(UIStrings.MaxAttempts, ref stack, "", 1))
        {
            // value has to be between 3 and 10
            LureStacks = Math.Clamp(stack, 1, 3);
            Service.Save();
        }

        DrawUtil.Checkbox(UIStrings.CancelAttempt, ref CancelAttempt);

        ConditionSet = ConditionUi.DrawConditionSetSlim(
            UIStrings.Conditions,
            ConditionSet,
            ConditionScope.AutoCast,
            showAdvanced: true,
            showSubPrefix: true);
    };

    public void TryCasting(bool lureSuccess)
    {
        if (!EzThrottler.Check("CastingLure"))
            return;

        if (Service.WorldState.GetStatusStacks(StatusId) >= LureStacks && CancelAttempt && !lureSuccess)
        {
            PlayerRes.CastActionDelayed(IDs.Actions.Rest);
            return;
        }

        if (!IsAvailableToCast() || lureSuccess)
            return;

        PlayerRes.CastActionDelayed(Id);
        EzThrottler.Throttle("CastingLure", 2500);
    }

    public override int Priority { get; set; } = 0;
    public override bool IsExcludedPriority { get; set; } = true;
}
