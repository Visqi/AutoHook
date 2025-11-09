using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using FFXIVClientStructs.FFXIV.Common.Math;
using Dalamud.Bindings.ImGui;

namespace AutoHook.Ui;

public class SubTabExtra
{
    private static CustomPresetConfig _preset = null!;

    public static void DrawExtraTab(CustomPresetConfig preset)
    {
        _preset = preset;
        var extraCfg = _preset.ExtraCfg;

        DrawHeader(extraCfg);

        if (extraCfg.Enabled || Service.Configuration.DontHideOptionsDisabled)
            DrawBody(extraCfg);
    }

    public static void DrawHeader(ExtraConfig config)
    {
        ImGui.Spacing();
        if (DrawUtil.Checkbox(UIStrings.Enable_Extra_Configs, ref config.Enabled))
        {
            if (config.Enabled)
            {
                if (_preset.IsGlobal && (Service.Configuration.HookPresets.SelectedPreset?.ExtraCfg.Enabled ?? false))
                {
                    Service.Configuration.HookPresets.SelectedPreset.ExtraCfg.Enabled = false;
                }
                else if (!_preset.IsGlobal)
                {
                    Service.Configuration.HookPresets.DefaultPreset.ExtraCfg.Enabled = false;
                }
            }

            Service.Save();
        }

        if (!_preset.IsGlobal)
        {
            if (Service.Configuration.HookPresets.DefaultPreset.ExtraCfg.Enabled && !config.Enabled)
                ImGui.TextColored(ImGuiColors.DalamudViolet, UIStrings.Global_Extra_Being_Used);
            else if (!config.Enabled)
                ImGui.TextColored(ImGuiColors.ParsedBlue, UIStrings.SubExtra_Disabled);
        }
        else
        {
            if (Service.Configuration.HookPresets.SelectedPreset?.ExtraCfg.Enabled ?? false)
                ImGui.TextColored(ImGuiColors.DalamudViolet,
                    string.Format(UIStrings.Custom_Extra_Being_Used,
                        Service.Configuration.HookPresets.SelectedPreset.PresetName));
            else if (!config.Enabled)
                ImGui.TextColored(ImGuiColors.ParsedBlue, UIStrings.SubExtra_Disabled);
        }

        ImGui.Spacing();
    }

    public static void DrawBody(ExtraConfig config)
    {
        using (var item = ImRaii.Child("###ExtraItems", new Vector2(0, 0), true))
        {
            ImGui.BeginGroup();

            ImGui.TextColored(ImGuiColors.DalamudYellow, UIStrings.BaitPresetPriorityWarning);

            DrawUtil.SpacingSeparator();

            DrawUtil.DrawCheckboxTree(UIStrings.ForceBaitSwap, ref config.ForceBaitSwap,
                () =>
                {
                    DrawUtil.TextV(UIStrings.SelectBaitStartFishing);
                    DrawUtil.DrawComboSelector(
                        GameRes.Baits,
                        bait => $"[#{bait.Id}] {bait.Name}",
                        $"{MultiString.GetItemName(config.ForcedBaitId)}",
                        bait => config.ForcedBaitId = bait.Id);
                }
            );

            DrawUtil.SpacingSeparator();

            if (ImGui.TreeNodeEx(UIStrings.FisherSIntuitionSettings, ImGuiTreeNodeFlags.FramePadding))
            {
                DrawFishersIntuition(config);
                ImGui.TreePop();
            }

            DrawUtil.SpacingSeparator();

            if (ImGui.TreeNodeEx(UIStrings.SpectralCurrentSettings, ImGuiTreeNodeFlags.FramePadding))
            {
                DrawSpectralCurrent(config);
                ImGui.TreePop();
            }

            DrawUtil.SpacingSeparator();

            if (ImGui.TreeNodeEx(UIStrings.AnglersArt, ImGuiTreeNodeFlags.FramePadding))
            {
                DrawAnglersArt(config);
                ImGui.TreePop();
            }

            DrawUtil.SpacingSeparator();

            if (ImGui.TreeNodeEx(UIStrings.SwimbaitSettings, ImGuiTreeNodeFlags.FramePadding))
            {
                DrawSwimbait(config);
                ImGui.TreePop();
            }

            DrawUtil.SpacingSeparator();

            if (DrawUtil.Checkbox(UIStrings.Reset_counter_after_swapping_presets, ref config.ResetCounterPresetSwap))
            {
                Service.Save();
            }

            ImGui.EndGroup();
        }
    }

    private static void DrawSpectralCurrent(ExtraConfig config)
    {
        ImGui.PushID(@"gaining_spectral");
        ImGui.TextColored(ImGuiColors.DalamudYellow, UIStrings.When_gaining_spectral_current);
        DrawPresetSwap(ref config.SwapPresetSpectralCurrentGain, ref config.PresetToSwapSpectralCurrentGain);
        DrawBaitSwap(ref config.SwapBaitSpectralCurrentGain, ref config.BaitToSwapSpectralCurrentGain);
        ImGui.PopID();

        ImGui.PushID(@"losing_spectral");
        ImGui.TextColored(ImGuiColors.DalamudYellow, UIStrings.When_losing_spectral_current);
        DrawPresetSwap(ref config.SwapPresetSpectralCurrentLost, ref config.PresetToSwapSpectralCurrentLost);
        DrawBaitSwap(ref config.SwapBaitSpectralCurrentLost, ref config.BaitToSwapSpectralCurrentLost);
        ImGui.PopID();
        DrawUtil.SpacingSeparator();
    }

    private static void DrawFishersIntuition(ExtraConfig config)
    {
        ImGui.PushID(@"gaining_intuition");
        ImGui.TextColored(ImGuiColors.DalamudYellow, UIStrings.When_gaining_fishers_intuition);

        DrawPresetSwap(ref config.SwapPresetIntuitionGain, ref config.PresetToSwapIntuitionGain);
        DrawBaitSwap(ref config.SwapBaitIntuitionGain, ref config.BaitToSwapIntuitionGain);
        ImGui.PopID();

        ImGui.PushID(@"losing_intuition");
        ImGui.TextColored(ImGuiColors.DalamudYellow, UIStrings.When_losing_fishers_intuition);
        DrawPresetSwap(ref config.SwapPresetIntuitionLost, ref config.PresetToSwapIntuitionLost);
        DrawBaitSwap(ref config.SwapBaitIntuitionLost, ref config.BaitToSwapIntuitionLost);

        if (DrawUtil.Checkbox(UIStrings.Quit_Fishing_On_IntuitionLost, ref config.QuitOnIntuitionLost))
            Service.Save();

        if (DrawUtil.Checkbox(UIStrings.Stop_Fishing_On_IntuitionLost, ref config.StopOnIntuitionLost))
            Service.Save();

        ImGui.PopID();
        DrawUtil.SpacingSeparator();
    }

    private static void DrawAnglersArt(ExtraConfig config)
    {
        ImGui.PushID(@"anglers_art");
        ImGui.TextColored(ImGuiColors.DalamudYellow, UIStrings.WhenAnglersAt);
        ImGui.SetNextItemWidth(90 * ImGuiHelpers.GlobalScale);
        if (ImGui.InputInt(UIStrings.StacksOrMore, ref config.AnglerStackQtd))
        {
            config.AnglerStackQtd = Math.Clamp(config.AnglerStackQtd, 0, 10);
            Service.Save();
        }

        DrawUtil.DrawCheckboxTree(UIStrings.StopQuitFishing, ref config.StopAfterAnglersArt,
            () =>
            {
                if (ImGui.RadioButton(UIStrings.Stop_Casting, config.AnglerStopFishingStep == FishingSteps.None))
                {
                    config.AnglerStopFishingStep = FishingSteps.None;
                    Service.Save();
                }

                ImGui.SameLine();
                ImGuiComponents.HelpMarker(UIStrings.Auto_Cast_Stopped);

                if (ImGui.RadioButton(UIStrings.Quit_Fishing, config.AnglerStopFishingStep == FishingSteps.Quitting))
                {
                    config.AnglerStopFishingStep = FishingSteps.Quitting;
                    Service.Save();
                }
            }
        );

        DrawPresetSwap(ref config.SwapPresetAnglersArt, ref config.PresetToSwapAnglersArt);
        DrawBaitSwap(ref config.SwapBaitAnglersArt, ref config.BaitToSwapAnglersArt);
        ImGui.PopID();
        DrawUtil.SpacingSeparator();
    }

    private static void DrawPresetSwap(ref bool enable, ref string presetName)
    {
        ImGui.PushID(@$"{nameof(DrawPresetSwap)}");

        var text = presetName;
        DrawUtil.DrawCheckboxTree(UIStrings.Swap_Preset, ref enable,
            () =>
            {
                DrawUtil.DrawComboSelector(
                    Service.Configuration.HookPresets.CustomPresets,
                    preset => preset.PresetName,
                    text,
                    preset => text = preset.PresetName);
            }
        );

        presetName = text;
        ImGui.PopID();
    }

    private static void DrawBaitSwap(ref bool enable, ref BaitFishClass baitSwap)
    {
        ImGui.PushID(@$"{nameof(DrawBaitSwap)}");

        var newBait = baitSwap;
        DrawUtil.DrawCheckboxTree(UIStrings.Swap_Bait, ref enable,
            () =>
            {
                DrawUtil.DrawComboSelector(
                    GameRes.Baits,
                    bait => $"[#{bait.Id}] {bait.Name}",
                    newBait.Name,
                    bait => newBait = bait);
            }
        );

        baitSwap = newBait;
        ImGui.PopID();
    }

    private static void DrawSwimbait(ExtraConfig config)
    {
        using var _ = ImRaii.PushId("DrawSwimbait");

        ImGui.PushID("swimbait_fills");
        ImGui.TextColored(ImGuiColors.DalamudYellow, UIStrings.WhenSwimbaitFills);
        ImGui.Spacing();

        ImGui.SetNextItemWidth(200 * ImGuiHelpers.GlobalScale);
        var fillsAction = (int)config.SwimbaitFillsAction;
        var actionOptions = new[]
        {
            UIStrings.None,
            UIStrings.Swap_Preset,
            UIStrings.Stop_Casting,
        };
        if (ImGui.Combo("###SwimbaitFillsAction", ref fillsAction, actionOptions, actionOptions.Length))
        {
            config.SwimbaitFillsAction = (SwimbaitAction)fillsAction;
            Service.Save();
        }

        if (config.SwimbaitFillsAction == SwimbaitAction.SwapPreset)
        {
            ImGui.Spacing();
            DrawUtil.DrawComboSelector(
                Service.Configuration.HookPresets.CustomPresets,
                preset => preset.PresetName,
                config.PresetToSwapSwimbaitFills,
                preset => config.PresetToSwapSwimbaitFills = preset.PresetName);
        }
        ImGui.PopID();

        ImGui.Spacing();
        DrawUtil.SpacingSeparator();
        ImGui.Spacing();

        ImGui.PushID("swimbait_runs_out");
        ImGui.TextColored(ImGuiColors.DalamudYellow, UIStrings.WhenSwimbaitIsOut);
        ImGui.Spacing();

        ImGui.SetNextItemWidth(200 * ImGuiHelpers.GlobalScale);
        var runsOutAction = (int)config.SwimbaitRunsOutAction;
        if (ImGui.Combo("###SwimbaitRunsOutAction", ref runsOutAction, actionOptions, actionOptions.Length))
        {
            config.SwimbaitRunsOutAction = (SwimbaitAction)runsOutAction;
            Service.Save();
        }

        if (config.SwimbaitRunsOutAction == SwimbaitAction.SwapPreset)
        {
            ImGui.Spacing();
            DrawUtil.DrawComboSelector(
                Service.Configuration.HookPresets.CustomPresets,
                preset => preset.PresetName,
                config.PresetToSwapSwimbaitRunsOut,
                preset => config.PresetToSwapSwimbaitRunsOut = preset.PresetName);
        }
        ImGui.PopID();
    }
}
