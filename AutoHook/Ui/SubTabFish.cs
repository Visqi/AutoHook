using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility.Raii;
using System.Numerics;

namespace AutoHook.Ui;

public class SubTabFish {
    private static CustomPresetConfig _preset = null!;

    public static void DrawFishTab(CustomPresetConfig presetCfg) {
        _preset = presetCfg;
        var listOfFish = presetCfg.ListOfFish;

        DrawDescription(listOfFish);

        using var item = ImRaii.Child("###FishItems", new Vector2(0, 0), false);
        for (var idx = 0; idx < listOfFish.Count; idx++) {
            var fish = listOfFish[idx];
            using var id = ImRaii.PushId($"fishTab###{idx}");

            var count = FishingCounters.GetFishCount(fish.UniqueId);
            var fishCount = count > 0 ? $"({UIStrings.Caught_Counter} {count})" : "";

            DrawUtil.DrawCheckboxHeader($"{fish.Fish.Name} {fishCount}", ref fish.Enabled, ImGuiTreeNodeFlags.FramePadding, () => {
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

                DrawUtil.DrawStopAfter(UIStrings.Stop_After_Caught, fish.StopAfterCaughtLimit, ref fish.StopFishingStep, ref fish.StopAfterResetCount);
                ImGui.Spacing();

                fish.NotifyOnSuccess.DrawConfig($"Fish caught: {fish.Fish.Name}!");
                ImGui.Spacing();

                fish.IgnoreConditionSet = ConditionUi.DrawConditionSet(UIStrings.IgnoreFishSettingWhen, fish.IgnoreConditionSet, ConditionScope.FishIgnore, showAdvanced: true);
            });

            ImGui.Spacing();
        }
    }

    private static void DrawDescription(List<FishConfig> list) {
        if (ImGui.Button(UIStrings.Add)) {
            if (list.All(x => x.Fish.Id != -1)) {
                list.Add(new FishConfig(new BaitFishClass()));
            }

            Configuration.Save();
        }

        ImGui.SameLine();
        ImGui.Text($"{UIStrings.Add_new_fish} ({list.Count})");

        ImGui.SameLine();

        var lastCatchFish = WorldState.Get().Fishing.LastCatch is { FishId: > 0 } lc
            ? GameRes.Fishes.FirstOrDefault(fish => fish.Id == lc.FishId) ?? new BaitFishClass(@"-", (int)lc.FishId) : null;

        if (ImGui.Button($"{UIStrings.AddLastCatch} {lastCatchFish?.Name ?? "-"}")) {
            if (lastCatchFish is null || lastCatchFish.Id is 0 or -1)
                return;
            if (list.Any(x => x.Fish.Id == lastCatchFish.Id))
                return;

            list.Add(new FishConfig(lastCatchFish));
            Configuration.Save();
        }
    }

    private static void DrawDeleteButton(FishConfig fishConfig) {
        ImGui.SameLine();
        using (ImRaii.PushFont(UiBuilder.IconFont)) {
            if (ImGuiComponents.IconButton(FontAwesomeIcon.Trash) && ImGui.GetIO().KeyShift) {
                _preset.RemoveItem(fishConfig.UniqueId);
                Configuration.Save();
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
        DrawUtil.DrawTreeNodeEx(UIStrings.SurfaceSlapIdenticalCast,
            () => DrawUtil.DrawSurfaceSlapAndIdenticalCast(fishConfig.SurfaceSlap, fishConfig.IdenticalCast));
    }

    private static void DrawMultihook(FishConfig fishConfig) {
        DrawUtil.DrawTreeNodeEx(UIStrings.Multihook_Settings, () => fishConfig.Multihook.DrawConfig());
    }

    private static void DrawMooch(FishConfig fishConfig) {
        DrawUtil.DrawTreeNodeEx(UIStrings.Mooch_Setting, () => {
            fishConfig.Mooch.SuppressHelpText = true;
            fishConfig.Mooch.DrawConfig();
            fishConfig.Mooch.SuppressHelpText = false;

            if (DrawUtil.Checkbox(UIStrings.Never_Mooch, ref fishConfig.NeverMooch, UIStrings.NeverMoochHelpText))
                fishConfig.Mooch.Enabled = false;
        });
    }

    private static void DrawSparefulHand(FishConfig fishConfig) {
        DrawUtil.DrawTreeNodeEx(UIStrings.SparefulHand_Settings, () => {
            fishConfig.SparefulHand.FishIdToCheck = (uint)fishConfig.Fish.Id;
            fishConfig.SparefulHand.DrawConfig();
        });
    }

    private static void DrawSwapBait(FishConfig fishConfig) {
        using var _ = ImRaii.PushId("DrawSwapBait");

        var alreadySwapped = "";
        if (FishingCounters.SwappedBait(fishConfig.UniqueId))
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
        if (FishingCounters.SwappedPreset(fishConfig.UniqueId))
            alreadySwapped = UIStrings.AlreadySwapped;

        DrawUtil.DrawCaughtCountLimitTree($"{UIStrings.Swap_Preset} {alreadySwapped}", fishConfig.SwapPresetLimit, () => DrawUtil.DrawPresetSwapSelector(fishConfig.PresetToSwap, preset => fishConfig.PresetToSwap = preset));
    }
}
