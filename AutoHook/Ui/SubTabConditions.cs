using AutoHook.Conditions;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility.Raii;

namespace AutoHook.Ui;

public static class SubTabConditions {
    public static void DrawConditionsTab(CustomPresetConfig preset) {
        ImGui.Spacing();
        ImGui.TextColored(ImGuiColors.DalamudYellow, UIStrings.PresetConditions_HelpText);
        ImGui.Spacing();

        var newlyAddedIndex = -1;
        if (ImGui.Button(UIStrings.Add)) {
            newlyAddedIndex = preset.NamedConditions.Count;
            preset.NamedConditions.Add(new NamedConditionConfig {
                Name = UIStrings.PresetConditions_NewName,
                ConditionSet = new(),
            });
            Service.Save();
        }

        ImGui.SameLine();
        ImGui.TextColored(ImGuiColors.DalamudGrey, $"({preset.NamedConditions.Count})");
        ImGui.Spacing();

        using var items = ImRaii.Child("###PresetConditions", new System.Numerics.Vector2(0, 0), true);
        for (var i = 0; i < preset.NamedConditions.Count; i++) {
            var named = preset.NamedConditions[i];
            named.EnsureUiId();
            using var id = ImRaii.PushId(named.UiId);

            var forceOpen = i == newlyAddedIndex;
            var removed = false;
            var header = string.IsNullOrWhiteSpace(named.Name) ? UIStrings.PresetConditions_NewName : named.Name;

            if (forceOpen)
                ImGui.SetNextItemOpen(true, ImGuiCond.Appearing);

            using (IsNamedConditionTrue(named) ? ImRaii.PushColor(ImGuiCol.Text, ImGuiColors.ParsedGreen) : null) {
                using var tree = ImRaii.TreeNode(header, ImGuiTreeNodeFlags.FramePadding | ImGuiTreeNodeFlags.SpanAvailWidth | ImGuiTreeNodeFlags.DefaultOpen);
                ImGui.TooltipOnHover(UIStrings.RightClickToRename);
                DrawNamedConditionContext(preset, named, ref removed);

                if (removed) {
                    i--;
                    continue;
                }

                if (!tree)
                    continue;

                ImGui.Spacing();
                var previousExclude = ConditionUi.ExcludeNamedConditionId;
                ConditionUi.ExcludeNamedConditionId = named.UniqueId;
                named.ConditionSet = ConditionUi.DrawConditionSet(UIStrings.Conditions, named.ConditionSet, ConditionScope.PresetDefinition, showPresets: true) ?? named.ConditionSet;
                ConditionUi.ExcludeNamedConditionId = previousExclude;
            }
        }
    }

    private static void DrawNamedConditionContext(CustomPresetConfig preset, NamedConditionConfig named, ref bool removed) {
        using var ctx = ImRaii.ContextPopupItem($"NamedConditionOptions###{named.UiId}");
        if (!ctx.Success)
            return;

        if (ImGui.Selectable(UIStrings.Rename, false, ImGuiSelectableFlags.DontClosePopups))
            ImGui.OpenPopup($"NamedConditionRename###{named.UiId}");

        DrawRenameNamedCondition(named);

        using (var disabled = ImRaii.Disabled(!ImGui.GetIO().KeyShift)) {
            if (ImGui.Selectable(UIStrings.Delete, false)) {
                preset.NamedConditions.Remove(named);
                Service.Save();
                removed = true;
            }
        }

        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.TooltipOnHover(UIStrings.HoldShiftToDelete);
    }

    private static void DrawRenameNamedCondition(NamedConditionConfig named) {
        using var popup = ImRaii.Popup($"NamedConditionRename###{named.UiId}");
        if (!popup.Success)
            return;

        ImGui.Text(UIStrings.EnterToConfirm);
        var name = named.Name;
        if (ImGui.InputText(UIStrings.PresetName, ref name, 64, ImGuiInputTextFlags.AutoSelectAll | ImGuiInputTextFlags.EnterReturnsTrue)) {
            named.Name = name;
            Service.Save();
            ImGui.CloseCurrentPopup();
        }

        if (ImGui.Button(UIStrings.Close)) {
            Service.Save();
            ImGui.CloseCurrentPopup();
        }
    }

    private static bool IsNamedConditionTrue(NamedConditionConfig named)
        => named.ConditionSet is { Groups.Count: > 0 } && named.ConditionSet.Evaluate(Service.WorldState, ConditionRegistry.Registry);
}
