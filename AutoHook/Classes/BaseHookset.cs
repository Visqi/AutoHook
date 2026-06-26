using AutoHook.Conditions;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility.Raii;
using StatusSheet = Lumina.Excel.Sheets.Status;

// ReSharper disable FieldCanBeMadeReadOnly.Global
// ReSharper disable MemberCanBePrivate.Global

namespace AutoHook.Classes;

public class BaseHookset(uint requiredStatus) {
    // for future use, maybe we need a hooking condition under a different status?
    public uint RequiredStatus = requiredStatus;

    private Guid _uniqueId;

    // Patience > Normal, Precision and Powerful
    public BaseBiteConfig PatienceWeak = new(HookType.Precision);
    public BaseBiteConfig PatienceStrong = new(HookType.Powerful);
    public BaseBiteConfig PatienceLegendary = new(HookType.Powerful);

    // Double Hook
    public bool UseDoubleHook;
    public bool LetFishEscapeDoubleHook;
    public BaseBiteConfig DoubleWeak = new(HookType.Double);
    public BaseBiteConfig DoubleStrong = new(HookType.Double);
    public BaseBiteConfig DoubleLegendary = new(HookType.Double);

    // Triple Hook
    public bool UseTripleHook;
    public bool LetFishEscapeTripleHook;
    public BaseBiteConfig TripleWeak = new(HookType.Triple);
    public BaseBiteConfig TripleStrong = new(HookType.Triple);
    public BaseBiteConfig TripleLegendary = new(HookType.Triple);

    // Timeout
    //public double TimeoutMin = 0;
    public double TimeoutMax = 0;
    public double ChumTimeoutMax = 0;
    public ConditionSet? TimeoutConditionSet { get; set; }
    public ConditionSet? ChumTimeoutConditionSet { get; set; }

    public bool UseCustomStatusHook;

    public AutoLures CastLures = new();

    public Guid GetUniqueId() {
        if (_uniqueId == Guid.Empty)
            _uniqueId = Guid.NewGuid();

        return _uniqueId;
    }

    public double GetEffectiveTimeoutMax(bool chumActive) {
        var timeout = chumActive ? ChumTimeoutMax : TimeoutMax;
        if (timeout <= 0)
            return 0;

        var set = chumActive ? ChumTimeoutConditionSet : TimeoutConditionSet;
        return set.Fails() ? 0 : timeout;
    }

    public void DrawOptions() {
        using var id = ImRaii.PushId(@"BaseHookset");
        if (RequiredStatus != 0) {
            ImGui.Spacing();
            var statusName = StatusSheet.GetRow(RequiredStatus).Name.ToString();
            DrawUtil.Checkbox(string.Format(UIStrings.UseConfigRequiredStatus, statusName), ref UseCustomStatusHook,
                UIStrings.RequiredStatusSettingHelpText);
        }

        DrawPatience();
        ImGui.Spacing();

        DrawDoubleHook();
        ImGui.Spacing();

        DrawTripleHook();
        ImGui.Spacing();

        DrawTimeout();
        ImGui.Spacing();

        DrawLures();
    }

    private void DrawPatience() {
        if (ImGui.TreeNodeEx(UIStrings.NormalPatienceHookset,
                ImGuiTreeNodeFlags.FramePadding | ImGuiTreeNodeFlags.AllowItemOverlap)) {
            PatienceWeak.DrawOptions(UIStrings.HookWeakExclamation, true);
            PatienceStrong.DrawOptions(UIStrings.HookStrongExclamation, true);
            PatienceLegendary.DrawOptions(UIStrings.HookLegendaryExclamation, true);
            ImGui.TreePop();
        }
    }

    private void DrawDoubleHook() {
        if (ImGui.TreeNodeEx(UIStrings.Double_Hook,
                ImGuiTreeNodeFlags.FramePadding | ImGuiTreeNodeFlags.AllowItemOverlap)) {
            DrawUtil.Checkbox(UIStrings.UseDoubleHook, ref UseDoubleHook);
            DrawUtil.Checkbox(UIStrings.LetTheFishEscape, ref LetFishEscapeDoubleHook, UIStrings.LetFishEscapeHelpText);
            ImGui.Separator();
            DoubleWeak.DrawOptions(UIStrings.HookWeakExclamation);
            DoubleStrong.DrawOptions(UIStrings.HookStrongExclamation);
            DoubleLegendary.DrawOptions(UIStrings.HookLegendaryExclamation);
            ImGui.TreePop();
        }
    }

    private void DrawTripleHook() {
        if (ImGui.TreeNodeEx(UIStrings.Triple_Hook,
                ImGuiTreeNodeFlags.FramePadding | ImGuiTreeNodeFlags.AllowItemOverlap)) {
            DrawUtil.Checkbox(UIStrings.UseTripleHook, ref UseTripleHook);
            DrawUtil.Checkbox(UIStrings.LetTheFishEscape, ref LetFishEscapeTripleHook, UIStrings.LetFishEscapeHelpText);
            ImGui.Separator();
            TripleWeak.DrawOptions(UIStrings.HookWeakExclamation);
            TripleStrong.DrawOptions(UIStrings.HookStrongExclamation);
            TripleLegendary.DrawOptions(UIStrings.HookLegendaryExclamation);
            ImGui.TreePop();
        }
    }

    private void DrawTimeout() {
        if (ImGui.TreeNodeEx(UIStrings.Timeout,
                ImGuiTreeNodeFlags.FramePadding | ImGuiTreeNodeFlags.AllowItemOverlap)) {
            ImGui.TextColored(ImGuiColors.DalamudYellow, UIStrings.TimeoutOption);
            DrawTimeoutField(UIStrings.TimeLimit, ref TimeoutMax, UIStrings.DoesntHaveAffectUnderChum);
            if (TimeoutMax > 0) {
                TimeoutConditionSet = Ui.ConditionUi.DrawConditionSet(UIStrings.Conditions, TimeoutConditionSet, Ui.ConditionScope.Hook, showAdvanced: true, showSubPrefix: true);
            }

            DrawTimeoutField(UIStrings.ChumTimeLimit, ref ChumTimeoutMax);
            if (ChumTimeoutMax > 0) {
                ChumTimeoutConditionSet = Ui.ConditionUi.DrawConditionSet(UIStrings.Conditions, ChumTimeoutConditionSet, Ui.ConditionScope.Hook, showAdvanced: true, showSubPrefix: true);
            }
            ImGui.TreePop();
        }
    }

    private static void DrawTimeoutField(string label, ref double timeoutMax, string? extraHelp = null) {
        ImGui.SetNextItemWidth(100.Scaled());
        if (ImGui.InputDouble(label, ref timeoutMax, .1, 1, @"%.1f%")) {
            switch (timeoutMax) {
                case 0.1:
                    timeoutMax = 2;
                    break;
                case <= 0:
                case <= 1.9: //This makes the option turn off if delay = 2 seconds when clicking the minus.
                    timeoutMax = 0;
                    break;
                case > 99:
                    timeoutMax = 99;
                    break;
            }

            Service.Save();
        }

        ImGui.SameLine();
        var help = extraHelp is null ? UIStrings.TimeoutHelpText : $"{UIStrings.TimeoutHelpText}\n\n{extraHelp}";
        ImGuiComponents.HelpMarker(help);
    }

    private void DrawLures() {
        using var id = ImRaii.PushId("Lures");

        CastLures.DrawConfig();
    }
}
