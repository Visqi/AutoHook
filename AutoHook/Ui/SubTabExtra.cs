using AutoHook.Conditions;
using AutoHook.Conditions.Definitions;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using FFXIVClientStructs.FFXIV.Common.Math;

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
        using var item = ImRaii.Child("###ExtraItems", new Vector2(0, 0), true);
        using (ImRaii.Group())
        {
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

            DrawTriggers(config);

            DrawUtil.SpacingSeparator();

            if (DrawUtil.Checkbox(UIStrings.Reset_counter_after_swapping_presets, ref config.ResetCounterPresetSwap))
                Service.Save();
        }
    }

    private static void DrawTriggers(ExtraConfig config)
    {
        ImGui.TextV(ImGuiColors.DalamudYellow, UIStrings.SwapStopRules);

        ImGui.SameLine();
        var newlyAddedIndex = -1;
        if (ImGui.SmallIconButton(FontAwesomeIcon.Plus))
        {
            newlyAddedIndex = config.Triggers.Count;
            config.Triggers.Add(new ExtraTrigger
            {
                ConditionSet = new ConditionSet(),
                SwapPreset = false,
                SwapBait = false,
                StopAction = ExtraStopAction.None,
            });
            Service.Save();
        }
        ImGui.TooltipOnHover(UIStrings.Add);

        for (var i = 0; i < config.Triggers.Count; i++)
        {
            var trig = config.Triggers[i];
            trig.EnsureUiId();
            using var id = ImRaii.PushId(trig.UiId);

            var headerLabel = GetTriggerHeaderLabel(i, trig);
            var enabled = trig.Enabled;
            var forceOpen = i == newlyAddedIndex;
            var removed = false;

            if (DrawUtil.DrawCheckboxHeader(headerLabel, ref enabled, ImGuiTreeNodeFlags.DefaultOpen, () =>
                {
                    // Slim editor with only Extra-relevant condition types (Intuition, Spectral, Angler's stacks, Swimbait).
                    trig.ConditionSet = ConditionUi.DrawConditionSetSlim(
                        "When",
                        trig.ConditionSet,
                        ConditionScope.Hook,
                        showAdvanced: true,
                        allowedTypeIds: [nameof(IntuitionActiveCD), nameof(SpectralActiveCD), nameof(StatusStacksCD), nameof(SwimbaitCountCD)],
                        drawHeaderExtras: () =>
                        {
                            ImGui.SameLine(0, 3);
                            if (ImGuiComponents.IconButton(FontAwesomeIcon.Trash))
                            {
                                config.Triggers.RemoveAt(i);
                                Service.Save();
                                removed = true;
                            }
                            ImGui.TooltipOnHover(UIStrings.Delete);
                        });

                    if (removed)
                        return;

                    ImGui.Separator();
                    ImGui.Indent(20 * ImGuiHelpers.GlobalScale);

                    var stopEnabled = trig.StopAction != ExtraStopAction.None;
                    DrawUtil.DrawCheckboxTree(UIStrings.StopQuitFishing, ref stopEnabled,
                        () =>
                        {
                            if (ImGui.RadioButton(UIStrings.Stop_Casting, trig.StopAction == ExtraStopAction.StopOnly))
                            {
                                trig.StopAction = ExtraStopAction.StopOnly;
                                Service.Save();
                            }

                            ImGui.SameLine();
                            ImGuiComponents.HelpMarker(UIStrings.Auto_Cast_Stopped);

                            if (ImGui.RadioButton(UIStrings.Quit_Fishing, trig.StopAction == ExtraStopAction.QuitFishing))
                            {
                                trig.StopAction = ExtraStopAction.QuitFishing;
                                Service.Save();
                            }
                        });

                    if (!stopEnabled && trig.StopAction != ExtraStopAction.None)
                    {
                        trig.StopAction = ExtraStopAction.None;
                        Service.Save();
                    }
                    else if (stopEnabled && trig.StopAction == ExtraStopAction.None)
                    {
                        trig.StopAction = ExtraStopAction.StopOnly;
                        Service.Save();
                    }

                    var swapPreset = trig.SwapPreset;
                    var presetName = trig.PresetToSwap;
                    DrawPresetSwap(ref swapPreset, ref presetName);
                    trig.SwapPreset = swapPreset;
                    trig.PresetToSwap = presetName;

                    var swapBait = trig.SwapBait;
                    var bait = trig.BaitToSwap;
                    DrawBaitSwap(ref swapBait, ref bait);
                    trig.SwapBait = swapBait;
                    trig.BaitToSwap = bait;

                    ImGui.Unindent(20 * ImGuiHelpers.GlobalScale);
                }, helpText: string.Empty, forceOpen: forceOpen))
            {
                trig.Enabled = enabled;
                Service.Save();
            }

            if (removed)
            {
                i--;
                continue;
            }
        }
    }

    private static string GetTriggerHeaderLabel(int index, ExtraTrigger trig)
    {
        var summary = SummarizeTrigger(trig);
        return string.IsNullOrEmpty(summary)
            ? $"Rule {index + 1}"
            : $"Rule {index + 1} – {summary}";
    }

    private static string SummarizeTrigger(ExtraTrigger trig)
    {
        if (trig.ConditionSet is not { Groups.Count: > 0 })
            return string.Empty;

        if (trig.ConditionSet.Groups.Count != 1)
            return string.Empty;

        var group = trig.ConditionSet.Groups[0];
        if (group.Conditions.Count != 1)
            return string.Empty;

        var cond = group.Conditions[0];
        var core = SummarizeCondition(cond);
        if (string.IsNullOrEmpty(core))
            return string.Empty;

        // For simple state-like conditions, infer OnGain / OnLose from "inv"
        var hasInv = cond.Params.TryGetValue("inv", out var invObj);
        var inv = false;
        if (hasInv)
        {
            if (invObj is bool b) inv = b;
            else if (invObj is long l) inv = l != 0;
        }

        var prefix = inv ? "OnLose " : "OnGain ";

        return prefix + core;
    }

    private static string SummarizeCondition(Condition cond)
    {
        switch (cond.TypeId)
        {
            case "IntuitionActive":
                return "Fisher's Intuition";
            case "SpectralActive":
                return "Spectral current";
            case "StatusStacks":
                {
                    if (cond.Params.TryGetValue("ids", out var idsObj) && idsObj is List<object> list && list.Count == 1)
                    {
                        var id = Convert.ToUInt32(list[0]);
                        if (id == IDs.Status.AnglersArt)
                        {
                            var stacks = 1;
                            if (cond.Params.TryGetValue("minStacks", out var msObj))
                                stacks = Convert.ToInt32(msObj);
                            return $"Angler's Art ≥ {stacks} stacks";
                        }
                    }
                    return "Status stacks";
                }
            case "SwimbaitCount":
                {
                    var v = 0;
                    if (cond.Params.TryGetValue("val", out var vObj))
                        v = Convert.ToInt32(vObj);
                    var above = true;
                    if (cond.Params.TryGetValue("above", out var aObj))
                    {
                        if (aObj is bool b) above = b;
                        else if (aObj is long l) above = l != 0;
                    }
                    var cmp = above ? "≥" : "≤";
                    return $"Swimbaits {cmp} {v}";
                }
            default:
                return string.Empty;
        }
    }

    private static void DrawPresetSwap(ref bool enable, ref string presetName)
    {
        using var _ = ImRaii.PushId(@$"{nameof(DrawPresetSwap)}");

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
    }

    private static void DrawBaitSwap(ref bool enable, ref BaitFishClass baitSwap)
    {
        using var _ = ImRaii.PushId(@$"{nameof(DrawBaitSwap)}");

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
    }
}
