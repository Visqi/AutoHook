using AutoHook.Conditions;
using AutoHook.Conditions.Definitions;
using AutoHook.Ui;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using System.Numerics;

namespace AutoHook.Ui;

public class SubTabFish {
    private static CustomPresetConfig _preset = null!;

    public static void DrawFishTab(CustomPresetConfig presetCfg) {
        _preset = presetCfg;
        var listOfFish = presetCfg.ListOfFish;

        DrawDescription(listOfFish);

        using var item = ImRaii.Child("###FishItems", new Vector2(0, 0), true);
        for (var idx = 0; idx < listOfFish.Count; idx++) {
            var fish = listOfFish[idx];
            using var id = ImRaii.PushId($"fishTab###{idx}");

            var count = FishingManager.FishingHelper.GetFishCount(fish.UniqueId);
            var fishCount = count > 0 ? $"({UIStrings.Caught_Counter} {count})" : "";

            DrawUtil.Checkbox($"###checkbox{idx}", ref fish.Enabled);

            ImGui.SameLine(0, 6 * ImGuiHelpers.GlobalScale);
            var x = ImGui.GetCursorPosX();
            if (ImGui.CollapsingHeader($"{fish.Fish.Name} {fishCount}###a{idx}")) {
                ImGui.SetCursorPosX(x);
                using (ImRaii.Group()) {
                    ImGui.Spacing();
                    DrawFishSearchBar(fish);
                    DrawDeleteButton(fish);
                    DrawUtil.SpacingSeparator();

                    DrawSurfaceSlapIdenticalCast(fish);
                    ImGui.Spacing();

                    DrawMultihook(fish);
                    ImGui.Spacing();

                    DrawMooch(fish);
                    ImGui.Spacing();

                    DrawSparefulHand(fish);
                    ImGui.Spacing();

                    DrawSwapBait(fish);
                    ImGui.Spacing();

                    DrawSwapPreset(fish);
                    ImGui.Spacing();

                    DrawStopAfter(fish);
                    ImGui.Spacing();

                    fish.NotifyOnSuccess.DrawConfig($"Fish caught: {fish.Fish.Name}!");
                    ImGui.Spacing();

                    fish.IgnoreConditionSet = ConditionUi.DrawConditionSetSlim(UIStrings.IgnoreFishSettingWhen, fish.IgnoreConditionSet, ConditionScope.FishIgnore, showAdvanced: true, showSubPrefix: true);

                }
            }

            ImGui.Spacing();
        }
    }

    private static void DrawDescription(List<FishConfig> list) {
        if (ImGui.Button(UIStrings.Add)) {
            if (list.All(x => x.Fish.Id != -1)) {
                list.Add(new FishConfig(new BaitFishClass()));
            }

            Service.Save();
        }

        ImGui.SameLine();
        ImGui.Text($"{UIStrings.Add_new_fish} ({list.Count})");

        ImGui.SameLine();

        if (ImGui.Button($"{UIStrings.AddLastCatch} {Service.LastCatch.Name ?? "-"}")) {
            if (Service.LastCatch.Id is 0 or -1)
                return;
            if (list.Any(x => x.Fish.Id == Service.LastCatch.Id))
                return;

            list.Add(new FishConfig(Service.LastCatch));
            Service.Save();
        }
    }

    private static void DrawDeleteButton(FishConfig fishConfig) {
        ImGui.SameLine();
        using (ImRaii.PushFont(UiBuilder.IconFont)) {
            if (ImGuiComponents.IconButton(FontAwesomeIcon.Trash) && ImGui.GetIO().KeyShift) {
                _preset.RemoveItem(fishConfig.UniqueId);
                Service.Save();
            }
        }

        ImGui.TooltipOnHover(UIStrings.HoldShiftToDelete);
    }

    private static void DrawFishSearchBar(FishConfig fishConfig) {
        using var _ = ImRaii.PushId("DrawFishSearchBar");
        DrawUtil.DrawComboSelector(
            GameRes.Fishes,
            fish => $"[#{fish.Id}] {fish.Name}",
            fishConfig.Fish.Name,
            fish => {
                fishConfig.Fish = fish;
                var stopLimit = fishConfig.StopAfterCaughtLimit.Value;
                fishConfig.StopAfterCaughtLimit.Value = stopLimit;
                var baitLimit = fishConfig.SwapBaitLimit.Value;
                fishConfig.SwapBaitLimit.Value = baitLimit;
                var presetLimit = fishConfig.SwapPresetLimit.Value;
                fishConfig.SwapPresetLimit.Value = presetLimit;
            });
    }

    private static void DrawSurfaceSlapIdenticalCast(FishConfig fishConfig) {
        using var _ = ImRaii.PushId($"{UIStrings.SurfaceSlapIdenticalCast}");

        if (ImGui.TreeNodeEx(UIStrings.SurfaceSlapIdenticalCast, ImGuiTreeNodeFlags.FramePadding)) {
            DrawUtil.DrawSurfaceSlapAndIdenticalCast(fishConfig.SurfaceSlap, fishConfig.IdenticalCast);
            ImGui.TreePop();
        }
    }

    private static void DrawMultihook(FishConfig fishConfig) {
        using var _ = ImRaii.PushId("DrawMultihook");
        using var tree = ImRaii.TreeNode(UIStrings.Multihook_Settings, ImGuiTreeNodeFlags.FramePadding);
        if (!tree) return;
        fishConfig.Multihook.DrawConfig();
    }

    private static void DrawMooch(FishConfig fishConfig) {
        using var _ = ImRaii.PushId(@"DrawMooch");
        if (ImGui.TreeNodeEx(UIStrings.Mooch_Setting, ImGuiTreeNodeFlags.FramePadding)) {
            fishConfig.Mooch.HelpText = string.Empty; // hack
            fishConfig.Mooch.DrawConfig();

            if (DrawUtil.Checkbox(UIStrings.Never_Mooch, ref fishConfig.NeverMooch, UIStrings.NeverMoochHelpText))
                fishConfig.Mooch.Enabled = false;

            ImGui.TreePop();
        }
    }

    private static void DrawSparefulHand(FishConfig fishConfig) {
        using var _ = ImRaii.PushId("DrawSparefulHand");
        using var tree = ImRaii.TreeNode(UIStrings.SparefulHand_Settings, ImGuiTreeNodeFlags.FramePadding);
        if (!tree) return;

        fishConfig.SparefulHand.FishIdToCheck = (uint)fishConfig.Fish.Id;
        fishConfig.SparefulHand.DrawConfig();
    }

    private static void DrawStopAfter(FishConfig fishConfig) {
        using var _ = ImRaii.PushId("DrawStopAfter");

        DrawUtil.DrawCaughtCountLimitTree(UIStrings.Stop_After_Caught, fishConfig.StopAfterCaughtLimit,
            () => {
                if (ImGui.RadioButton(UIStrings.Stop_Casting, fishConfig.StopFishingStep == FishingSteps.None)) {
                    fishConfig.StopFishingStep = FishingSteps.None;
                    Service.Save();
                }

                ImGui.SameLine();
                ImGuiComponents.HelpMarker(UIStrings.Auto_Cast_Stopped);

                if (ImGui.RadioButton(UIStrings.Quit_Fishing, fishConfig.StopFishingStep == FishingSteps.Quitting)) {
                    fishConfig.StopFishingStep = FishingSteps.Quitting;
                    Service.Save();
                }

                ImGui.SameLine();
                ImGuiComponents.HelpMarker(UIStrings.Quit_Action_HelpText);

                DrawUtil.Checkbox(UIStrings.Reset_the_counter, ref fishConfig.StopAfterResetCount);
            });
    }

    private static void DrawSwapBait(FishConfig fishConfig) {
        using var _ = ImRaii.PushId("DrawSwapBait");

        var alreadySwapped = "";
        if (FishingManager.FishingHelper.SwappedBait(fishConfig.UniqueId))
            alreadySwapped = UIStrings.AlreadySwapped;

        DrawUtil.DrawCaughtCountLimitTree($"{UIStrings.Swap_Bait} {alreadySwapped}", fishConfig.SwapBaitLimit,
            () => {
                DrawUtil.DrawBaitSwapSelector(fishConfig.BaitToSwap, bait => fishConfig.BaitToSwap = bait);
                DrawUtil.Checkbox(UIStrings.Reset_the_counter, ref fishConfig.SwapBaitResetCount);
            });
    }

    private static void DrawSwapPreset(FishConfig fishConfig) {
        using var _ = ImRaii.PushId("DrawSwapPreset");

        var alreadySwapped = "";
        if (FishingManager.FishingHelper.SwappedPreset(fishConfig.UniqueId))
            alreadySwapped = UIStrings.AlreadySwapped;

        DrawUtil.DrawCaughtCountLimitTree($"{UIStrings.Swap_Preset} {alreadySwapped}", fishConfig.SwapPresetLimit, () => DrawUtil.DrawPresetSwapSelector(fishConfig.PresetToSwap, preset => fishConfig.PresetToSwap = preset));
    }
}
