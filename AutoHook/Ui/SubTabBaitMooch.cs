using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using System.Numerics;

namespace AutoHook.Ui;

public class SubTabBaitMooch
{
    private static CustomPresetConfig _preset = null!;

    public static void DrawHookTab(CustomPresetConfig preset)
    {
        _preset = preset;
        using var mainTab = ImRaii.TabBar(@"TabBarHooking", ImGuiTabBarFlags.NoTooltip);
        if (!mainTab)
            return;

        using (var tabBait = ImRaii.TabItem(UIStrings.Bait))
        {
            DrawUtil.HoveredTooltip(UIStrings.BaitTabHelpText);
            if (tabBait)
                DrawBody(preset.ListOfBaits, false);
        }

        using var tabMooch = ImRaii.TabItem(UIStrings.Mooch);
        DrawUtil.HoveredTooltip(UIStrings.MoochTabHelpText);
        if (tabMooch)
            DrawBody(preset.ListOfMooch, true);
    }

    private static void DrawBody(List<HookConfig> list, bool isMooch)
    {
        if (!_preset.IsGlobal)
        {
            ImGui.Spacing();

            if (ImGui.Button(UIStrings.Add))
            {
                if (list.All(x => x.BaitFish.Id != -1))
                {
                    list.Add(new HookConfig(new BaitFishClass()));
                    Service.Save();
                }
            }

            var bait = isMooch ? UIStrings.Add_new_mooch : UIStrings.Add_new_bait;

            ImGui.SameLine();
            ImGui.Text(@$"{bait} ({list.Count})");
            ImGui.SameLine();
            ImGuiComponents.HelpMarker(UIStrings.TabPresets_DrawHeader_CorrectlyEditTheBaitMoochName);
            ImGui.Spacing();
        }

        using var items = ImRaii.Child($"###BaitMoochItems", Vector2.Zero, false);
        for (var idx = 0; idx < list?.Count; idx++)
        {
            var hook = list[idx];
            using var id = ImRaii.PushId(@$"id###{idx}");

            var baitName = !_preset.IsGlobal ? hook.BaitFish.Name :
                isMooch ? UIStrings.All_Mooches : UIStrings.All_Baits;

            var count = FishingManager.FishingHelper.GetFishCount(hook.UniqueId);
            var hookCounter = count > 0 ? @$"({UIStrings.Hooked_Counter} {count})" : "";

            if (DrawUtil.DrawCheckboxHeader(@$"{baitName} {hookCounter}###{idx}", ref hook.Enabled, ImGuiTreeNodeFlags.FramePadding, () =>
                {
                    if (!_preset.IsGlobal)
                    {
                        ImGui.Spacing();
                        DrawInputSearchBar(hook, isMooch);
                        ImGui.SameLine();
                        DrawDeleteButton(hook);
                        ImGui.Spacing();
                    }

                    //rewrite TabBarsBaitMooch using ImRaii
                    using (var tabBarsBaitMooch = ImRaii.TabBar(@"TabBarsBaitMooch", ImGuiTabBarFlags.NoTooltip))
                    {
                        if (tabBarsBaitMooch)
                        {
                            using (var tabDefault = ImRaii.TabItem($"{UIStrings.DefaultSubTab}###Default"))
                            {
                                if (tabDefault)
                                    hook.NormalHook.DrawOptions();
                            }

                            using var tabIntuition = ImRaii.TabItem($"{UIStrings.Intuition}###Intuition");
                            if (tabIntuition)
                                hook.IntuitionHook.DrawOptions();
                        }
                    }

                    if (isMooch)
                    {
                        ImGui.Spacing();
                        if (_preset.IsGlobal || hook.BaitFish.Id == GameRes.AllMoochesId || GameRes.MoochableFish.Any(f => f.Id == hook.BaitFish.Id))
                            DrawSwimbaitUsage(hook);
                    }
                }, UIStrings.EnabledConfigArrowhelpMarker))
            {
                Service.Save();
            }

            DrawUtil.SpacingSeparator();
        }
    }

    private static void DrawInputSearchBar(HookConfig hookConfig, bool isMooch)
    {
        var list = (isMooch ? GameRes.Fishes : GameRes.Baits).ToList();
        if (isMooch)
            list.Insert(0, new BaitFishClass(UIStrings.All_Mooches, GameRes.AllMoochesId));
        else
            list.Insert(0, new BaitFishClass(UIStrings.All_Baits, GameRes.AllBaitsId));

        DrawUtil.DrawComboSelector(
            list,
            item => $"[{item.Id}] {item.Name}",
            hookConfig.BaitFish.Name,
            item => hookConfig.BaitFish = item);

        if (isMooch)
            return;

        ImGui.SameLine();

        if (ImGuiComponents.IconButton(FontAwesomeIcon.ArrowLeft))
        {
            if (Service.WorldState.CurrentBaitId > 0) // just make sure bait is bait
                hookConfig.BaitFish = list.Single(x => x.Id == Service.WorldState.CurrentBaitId);
        }

        ImGui.TooltipOnHover(UIStrings.UIUseCurrentBait);
    }

    private static void DrawDeleteButton(HookConfig hookConfig)
    {
        if (_preset.IsGlobal)
            return;

        using (ImRaii.Disabled(!ImGui.GetIO().KeyShift))
        {
            if (ImGuiComponents.IconButton(FontAwesomeIcon.Trash))
            {
                _preset.RemoveItem(hookConfig.UniqueId);
                Service.Save();
            }
        }

        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip(UIStrings.HoldShiftToDelete);
    }

    private static void DrawSwimbaitUsage(HookConfig hookConfig)
    {
        using var _ = ImRaii.PushId("DrawSwimbaitUsage");

        var isGlobal = _preset.IsGlobal;

        if (ImGui.TreeNodeEx(UIStrings.UseSwimbait, ImGuiTreeNodeFlags.FramePadding))
        {
            var enableText = isGlobal ? UIStrings.EnableUsingSwimbaitGlobal : UIStrings.EnableUsingSwimbait;
            var helpText = isGlobal ? UIStrings.UseSwimbaitHelpTextGlobal : UIStrings.UseSwimbaitHelpText;

            if (DrawUtil.Checkbox(enableText, ref hookConfig.UseSwimbait, helpText))
                Service.Save();

            if (hookConfig.UseSwimbait)
            {
                ImGui.Spacing();

                var countText = isGlobal ? UIStrings.OnlyUseWhenSwimbaitCountGlobal : UIStrings.OnlyUseWhenSwimbaitCount;
                var countHelpText = isGlobal ? UIStrings.OnlyUseWhenSwimbaitCountHelpTextGlobal : UIStrings.OnlyUseWhenSwimbaitCountHelpText;

                DrawUtil.DrawWordWrappedString(countText);
                ImGui.SameLine();
                ImGui.SetNextItemWidth(90 * ImGuiHelpers.GlobalScale);
                var threshold = hookConfig.SwimbaitCountThreshold;
                if (ImGui.InputInt("###SwimbaitThreshold", ref threshold, 1, 1))
                {
                    threshold = Math.Clamp(threshold, 1, 3);
                    hookConfig.SwimbaitCountThreshold = threshold;
                    Service.Save();
                }

                ImGui.TooltipOnHover(countHelpText);

                ImGui.Spacing();

                var onlyWhenNoMooch = hookConfig.OnlyUseWhenNoMoochAvailable.Value;
                if (DrawUtil.Checkbox(UIStrings.OnlyUseWhenNoMoochAvailable, ref onlyWhenNoMooch, UIStrings.OnlyUseWhenNoMoochAvailableHelpText))
                {
                    hookConfig.OnlyUseWhenNoMoochAvailable.Value = onlyWhenNoMooch;
                    Service.Save();
                }
            }

            ImGui.TreePop();
        }
    }
}
