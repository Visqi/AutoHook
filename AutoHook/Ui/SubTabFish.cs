using System.Numerics;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Bindings.ImGui;

namespace AutoHook.Ui;

public class SubTabFish
{
    private static CustomPresetConfig _preset = null!;

    public static void DrawFishTab(CustomPresetConfig presetCfg)
    {
        _preset = presetCfg;
        var listOfFish = presetCfg.ListOfFish;

        DrawDescription(listOfFish);

        using (var item = ImRaii.Child("###FishItems", new Vector2(0, 0), true))
        {
            for (var idx = 0; idx < listOfFish.Count; idx++)
            {
                var fish = listOfFish[idx];
                ImGui.PushID($"fishTab###{idx}");

                var count = FishingManager.FishingHelper.GetFishCount(fish.UniqueId);
                var fishCount = count > 0 ? $"({UIStrings.Caught_Counter} {count})" : "";

                if (DrawUtil.Checkbox($"###checkbox{idx}", ref fish.Enabled))
                    Service.Save();

                ImGui.SameLine(0, 6);
                var x = ImGui.GetCursorPosX();
                if (ImGui.CollapsingHeader($"{fish.Fish.Name} {fishCount}###a{idx}"))
                {
                    ImGui.SetCursorPosX(x);
                    ImGui.BeginGroup();
                    ImGui.Spacing();
                    DrawFishSearchBar(fish);
                    DrawDeleteButton(fish);
                    DrawUtil.SpacingSeparator();

                    DrawSurfaceSlapIdenticalCast(fish);
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

                    if (DrawUtil.Checkbox(UIStrings.Ignore_When_Intuition, ref fish.IgnoreOnIntuition))
                        Service.Save();

                    ImGui.EndGroup();
                }

                ImGui.Spacing();
                ImGui.PopID();
            }
        }
    }

    private static void DrawDescription(List<FishConfig> list)
    {
        if (ImGui.Button(UIStrings.Add))
        {
            if (list.All(x => x.Fish.Id != -1))
            {
                list.Add(new FishConfig(new BaitFishClass()));
            }

            Service.Save();
        }

        ImGui.SameLine();
        ImGui.Text($"{UIStrings.Add_new_fish} ({list.Count})");
        ImGui.SameLine();

        ImGui.SameLine();

        if (ImGui.Button($"{UIStrings.AddLastCatch} {Service.LastCatch.Name ?? "-"}"))
        {
            if (Service.LastCatch.Id is 0 or (-1))
                return;
            if (list.Any(x => x.Fish.Id == Service.LastCatch.Id))
                return;

            list.Add(new FishConfig(Service.LastCatch));
            Service.Save();
        }
    }

    private static void DrawDeleteButton(FishConfig fishConfig)
    {
        ImGui.SameLine();
        ImGui.PushFont(UiBuilder.IconFont);
        if (ImGuiComponents.IconButton(FontAwesomeIcon.Trash) && ImGui.GetIO().KeyShift)
        {
            _preset.RemoveItem(fishConfig.UniqueId);
            Service.Save();
        }

        ImGui.PopFont();

        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(UIStrings.HoldShiftToDelete);
    }

    private static void DrawFishSearchBar(FishConfig fishConfig)
    {
        ImGui.PushID("DrawFishSearchBar");
        DrawUtil.DrawComboSelector(
            GameRes.Fishes,
            (BaitFishClass fish) => $"[#{fish.Id}] {fish.Name}",
            fishConfig.Fish.Name,
            (BaitFishClass fish) => fishConfig.Fish = fish);

        ImGui.PopID();
    }

    private static void DrawSurfaceSlapIdenticalCast(FishConfig fishConfig)
    {
        ImGui.PushID($"{UIStrings.SurfaceSlapIdenticalCast}");

        if (ImGui.TreeNodeEx(UIStrings.SurfaceSlapIdenticalCast, ImGuiTreeNodeFlags.FramePadding))
        {
            fishConfig.SurfaceSlap.DrawConfig();

            fishConfig.IdenticalCast.DrawConfig();

            ImGui.TreePop();
        }

        ImGui.PopID();
    }

    private static void DrawMooch(FishConfig fishConfig)
    {
        ImGui.PushID(@"DrawMooch");
        if (ImGui.TreeNodeEx(UIStrings.Mooch_Setting, ImGuiTreeNodeFlags.FramePadding))
        {
            fishConfig.Mooch.DrawConfig();

            if (DrawUtil.Checkbox(UIStrings.Never_Mooch, ref fishConfig.NeverMooch, UIStrings.NeverMoochHelpText))
            {
                fishConfig.Mooch.Enabled = false;
                Service.Save();
            }

            ImGui.TreePop();
        }

        ImGui.PopID();
    }

    private static void DrawSparefulHand(FishConfig fishConfig)
    {
        using var _ = ImRaii.PushId("DrawSparefulHand");
        using var tree = ImRaii.TreeNode(UIStrings.SparefulHand_Settings, ImGuiTreeNodeFlags.FramePadding);
        if (!tree) return;

        fishConfig.SparefulHand.FishIdToCheck = (uint)fishConfig.Fish.Id;
        fishConfig.SparefulHand.DrawConfig();

        ImGui.Spacing();

        DrawUtil.DrawWordWrappedString(UIStrings.OnlyUseIfSwimbaitCountLessThan);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(90 * ImGuiHelpers.GlobalScale);
        var swimbaitLimit = fishConfig.SparefulHand.SwimbaitCountLimit;
        if (ImGui.InputInt("###SwimbaitLimit", ref swimbaitLimit, 1, 1))
        {
            swimbaitLimit = Math.Clamp(swimbaitLimit, 0, 3);
            fishConfig.SparefulHand.SwimbaitCountLimit = swimbaitLimit;
            Service.Save();
        }

        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(UIStrings.OnlyUseIfSwimbaitCountLessThanHelpText);
    }

    private static void DrawSwapBait(FishConfig fishConfig)
    {
        using var _ = ImRaii.PushId("DrawSwapBait");

        var alreadySwapped = "";
        if (FishingManager.FishingHelper.SwappedBait(fishConfig.UniqueId))
            alreadySwapped = UIStrings.AlreadySwapped;

        DrawUtil.DrawCheckboxTree($"{UIStrings.Swap_Bait} {alreadySwapped}", ref fishConfig.SwapBait,
            () =>
            {
                DrawUtil.DrawComboSelector(
                    GameRes.Baits,
                    bait => $"[#{bait.Id}] {bait.Name}",
                    fishConfig.BaitToSwap.Name,
                    bait => fishConfig.BaitToSwap = bait);

                ImGui.Spacing();

                DrawUtil.DrawWordWrappedString(UIStrings.AfterBeingCaught);

                ImGui.SameLine();
                ImGui.SetNextItemWidth(90 * ImGuiHelpers.GlobalScale);
                if (ImGui.InputInt(UIStrings.TimeS, ref fishConfig.SwapBaitCount))
                {
                    if (fishConfig.SwapBaitCount < 1)
                        fishConfig.SwapBaitCount = 1;

                    Service.Save();
                }
                DrawUtil.Checkbox(UIStrings.Reset_Counter_Bait_Swap, ref fishConfig.SwapBaitResetCount);
            }
        );
    }

    private static void DrawSwapPreset(FishConfig fishConfig)
    {
        using var _ = ImRaii.PushId("DrawSwapPreset");

        var alreadySwapped = "";
        if (FishingManager.FishingHelper.SwappedPreset(fishConfig.UniqueId))
            alreadySwapped = UIStrings.AlreadySwapped;
        DrawUtil.DrawCheckboxTree($"{UIStrings.Swap_Preset} {alreadySwapped}", ref fishConfig.SwapPresets,
            () =>
            {
                DrawUtil.DrawComboSelector(
                    Service.Configuration.HookPresets.CustomPresets,
                    preset => preset.PresetName,
                    fishConfig.PresetToSwap,
                    preset => fishConfig.PresetToSwap = preset.PresetName);

                ImGui.Spacing();

                DrawUtil.DrawWordWrappedString(UIStrings.AfterBeingCaught);

                ImGui.SameLine();
                ImGui.SetNextItemWidth(90 * ImGuiHelpers.GlobalScale);
                if (ImGui.InputInt(UIStrings.TimeS, ref fishConfig.SwapPresetCount))
                {
                if (fishConfig.SwapPresetCount < 1)
                    fishConfig.SwapPresetCount = 1;

                    Service.Save();
                }
            }
        );
    }

    private static void DrawStopAfter(FishConfig fishConfig)
    {
        using var _ = ImRaii.PushId("DrawStopAfter");

        DrawUtil.DrawCheckboxTree(UIStrings.Stop_After_Caught, ref fishConfig.StopAfterCaught,
            () =>
            {
                ImGui.SetNextItemWidth(90 * ImGuiHelpers.GlobalScale);
                if (ImGui.InputInt(UIStrings.TimeS, ref fishConfig.StopAfterCaughtLimit))
                {
                    if (fishConfig.StopAfterCaughtLimit < 1)
                        fishConfig.StopAfterCaughtLimit = 1;

                    Service.Save();
                }

                if (ImGui.RadioButton(UIStrings.Stop_Casting, fishConfig.StopFishingStep == FishingSteps.None))
                {
                    fishConfig.StopFishingStep = FishingSteps.None;
                    Service.Save();
                }

                ImGui.SameLine();
                ImGuiComponents.HelpMarker(UIStrings.Auto_Cast_Stopped);

                if (ImGui.RadioButton(UIStrings.Quit_Fishing, fishConfig.StopFishingStep == FishingSteps.Quitting))
                {
                    fishConfig.StopFishingStep = FishingSteps.Quitting;
                    Service.Save();
                }

                ImGui.SameLine();
                ImGuiComponents.HelpMarker(UIStrings.Quit_Action_HelpText);

                DrawUtil.Checkbox(UIStrings.Reset_the_counter, ref fishConfig.StopAfterResetCount);
            });
    }
}
