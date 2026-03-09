using AutoHook.Conditions;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using ECommons.Throttlers;
using FFXIVClientStructs.FFXIV.Client.Game;
using System.ComponentModel;
using System.Numerics;

namespace AutoHook.Classes;

public abstract class BaseActionCast {
    protected BaseActionCast(string name, uint id, ActionType actionType = ActionType.Action) {
        Name = name;
        Id = id;
        Enabled = false;

        ActionType = actionType;

        if (actionType == ActionType.Action && id != IDs.Actions.ThaliaksFavor)
            GpThreshold = (int)PlayerRes.CastActionCost(Id, ActionType);
    }

    [NonSerialized] public string Name;

    [NonSerialized] public string HelpText = @"";

    [DefaultValue(false)] public bool Enabled;

    public uint Id;

    public int GpThreshold;

    [NonSerialized] public bool IsSpearFishing;

    [DefaultValue(true)] public bool GpThresholdAbove { get; set; } = true;

    public virtual bool DoesCancelMooch() => false;

    [DefaultValue(true)] public bool DontCancelMooch = true;

    public virtual bool RequiresAutoCastAvailabl() => false;

    public virtual bool RequiresTimeWindow() => false;

    public virtual int Priority { get; set; }

    [NonSerialized] public ActionType ActionType;

    public ConditionSet? ConditionSet { get; set; }

    protected bool EvaluateConditionSet()
        => ConditionSet is not { Groups.Count: > 0 }
           || ConditionSet.Evaluate(Service.WorldState, ConditionRegistry.Registry);

    public virtual void SetThreshold(int newCost) {
        var actionCost = Id == IDs.Actions.ThaliaksFavor ? 0 : (int)PlayerRes.CastActionCost(Id, ActionType);
        GpThreshold = (newCost < 0) ? 0 : Math.Max(newCost, actionCost);
        Service.Save();
    }

    public bool IsAvailableToCast(bool ignoreCurrentMooch = false) {
        if (!Enabled)
            return false;

        if (DoesCancelMooch() && Service.WorldState.IsMoochAvailable() && DontCancelMooch && !ignoreCurrentMooch) {
            return false;
        }

        var condition = CastCondition();
        var currentGp = Service.WorldState.CurrentGp;
        var hasGp = GpThresholdAbove ? currentGp >= GpThreshold : currentGp <= GpThreshold;
        var actionAvailable = Service.WorldState.ActionAvailable(Id, ActionType);

        if (EzThrottler.Throttle("LogActions", 1000))
            Service.PrintVerbose(
                @$"[BaseAction] {Name} - GpCheck:{hasGp}, ActionAvailable: {actionAvailable}, OtherConditions: {condition}");

        return hasGp && actionAvailable && condition;
    }

    public abstract bool CastCondition();

    public virtual string GetName() => "";

    public virtual int GetPriority() => Priority;

    protected delegate void DrawOptionsDelegate();

    protected virtual DrawOptionsDelegate? DrawOptions => null;

    public abstract bool IsExcludedPriority { get; set; }

    public virtual void DrawConfig(List<BaseActionCast>? availableActs = null)
        => DrawConfigWithLabel(GetName(), availableActs);

    public void DrawConfigWithLabel(string label, List<BaseActionCast>? availableActs = null) {
        using var cfgId = ImRaii.PushId(@$"{label}_cfg");

        if (DrawOptions != null) {
            if (DrawUtil.Checkbox(@$"###{label}", ref Enabled, HelpText, true)) {
                Service.PrintDebug(@$"[BaseAction] {Name} - {(Enabled ? @"Enabled" : @"Disabled")}");
                Service.Save();
            }

            ImGui.SameLine(0, 3);

            var x = ImGui.GetCursorPosX();
            if (ImGui.TreeNodeEx(label, ImGuiTreeNodeFlags.FramePadding)) {
                ImGui.SameLine(200 * ImGui.GetIO().FontGlobalScale * (ImGui.GetFontSize() / 12f));
                DrawGpThreshold();
                DrawUpDownArrows(availableActs);
                ImGui.SetCursorPosX(x);
                using (ImRaii.Group()) {
                    DrawOptions?.Invoke();
                    ImGui.Separator();
                }
                ImGui.TreePop();
            }
            else {
                ImGui.SameLine(200 * ImGui.GetIO().FontGlobalScale * (ImGui.GetFontSize() / 12f));
                DrawGpThreshold();
                DrawUpDownArrows(availableActs);
            }
        }
        else {
            if (DrawUtil.Checkbox(@$"###{label}", ref Enabled, HelpText, true)) {
                Service.PrintDebug(@$"[BaseAction] {Name} - {(Enabled ? @"Enabled" : @"Disabled")}");
                Service.Save();
            }

            ImGui.SameLine(0, 28);
            ImGui.Text(label);
            ImGui.SameLine(200 * ImGui.GetIO().FontGlobalScale * (ImGui.GetFontSize() / 12f));
            DrawGpThreshold();
            DrawUpDownArrows(availableActs);
        }
    }

    public virtual void DrawConfigOptions() {
        DrawOptions?.Invoke();
    }

    private void DrawUpDownArrows(List<BaseActionCast>? availableActs) {
        if (availableActs is null || IsExcludedPriority) return;

        if (GetPriority() == 0) //failsafe I guess
            Priority = availableActs.MaxBy(x => x.Priority)!.Priority + 1;

        ImGui.NextColumn();

        ImGui.SameLine();

        if (!availableActs.Any(x => x.Priority < Priority && !x.IsExcludedPriority))
            ImGui.BeginDisabled();

        if (ImGui.ArrowButton(@"###UpArrow", ImGuiDir.Up)) {
            if (availableActs.Any(x => x.Priority < Priority && !x.IsExcludedPriority)) {
                var nextAct = availableActs.Where(x => x.Priority < Priority && !x.IsExcludedPriority)
                    .OrderByDescending(x => x.Priority).First();
                nextAct.Priority = Priority;
                Priority--;
            }
        }

        if (!availableActs.Any(x => x.Priority < Priority && !x.IsExcludedPriority))
            ImGui.EndDisabled();

        ImGui.SameLine();

        if (!availableActs.Any(x => x.Priority > Priority && !x.IsExcludedPriority))
            ImGui.BeginDisabled();

        if (ImGui.ArrowButton(@"###DownArrow", ImGuiDir.Down)) {
            if (availableActs.Any(x => x.Priority > Priority && !x.IsExcludedPriority)) {
                var lastAct = availableActs.Where(x => x.Priority > Priority && !x.IsExcludedPriority)
                    .OrderBy(x => x.Priority).First();
                lastAct.Priority = Priority;
                Priority++;
            }
        }

        if (!availableActs.Any(x => x.Priority > Priority && !x.IsExcludedPriority))
            ImGui.EndDisabled();
    }

    public virtual void DrawGpThreshold() {
        using var gpId = ImRaii.PushId(@$"{GetName()}_gp");
        if (ImGui.Button(UIStrings.GPlabel)) {
            ImGui.OpenPopup(strId: @"gp_cfg");
        }

        using var popup = ImRaii.Popup(@"gp_cfg");
        if (!popup.Success) return;

        using var item = ImRaii.Child("###gp_cfg2", new Vector2(175, 125), true);
        if (ImGui.Button(@" X "))
            ImGui.CloseCurrentPopup();
        ImGui.SameLine();
        ImGui.TextColored(ImGuiColors.DalamudYellow, @$"GP - {GetName()}");

        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(
                @$"{GetName()} {UIStrings.WillBeUsedWhenYourGPIsEqualOr} {(GpThresholdAbove ? UIStrings.Above : UIStrings.Below)} {GpThreshold}");

        ImGui.Separator();
        if (ImGui.RadioButton(UIStrings.Above, GpThresholdAbove)) {
            GpThresholdAbove = true;
            Service.Save();
        }

        //ImGui.SameLine();

        if (ImGui.RadioButton(UIStrings.Below, !GpThresholdAbove)) {
            GpThresholdAbove = false;
            Service.Save();
        }

        //ImGui.SameLine();

        ImGui.SetNextItemWidth(100 * ImGuiHelpers.GlobalScale);
        if (ImGui.InputInt(UIStrings.GP, ref GpThreshold, 1, 1)) {
            GpThreshold = Math.Max(GpThreshold, 0);
            SetThreshold(GpThreshold);
            Service.Save();
        }
    }
}
