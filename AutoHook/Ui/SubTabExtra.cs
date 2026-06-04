using AutoHook.Conditions;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility.Raii;
using FFXIVClientStructs.FFXIV.Common.Math;

namespace AutoHook.Ui;

public class SubTabExtra {
    private static CustomPresetConfig _preset = null!;

    public static void DrawExtraTab(CustomPresetConfig preset) {
        _preset = preset;
        var extraCfg = _preset.ExtraCfg;

        DrawHeader(extraCfg);

        if (extraCfg.Enabled || Service.Configuration.DontHideOptionsDisabled)
            DrawBody(extraCfg);
    }

    public static void DrawHeader(ExtraConfig config) {
        ImGui.Spacing();
        if (DrawUtil.Checkbox(UIStrings.Enable_Extra_Configs, ref config.Enabled)) {
            if (config.Enabled) {
                if (_preset.IsGlobal && (Service.Configuration.HookPresets.SelectedPreset?.ExtraCfg.Enabled ?? false)) {
                    Service.Configuration.HookPresets.SelectedPreset.ExtraCfg.Enabled = false;
                }
                else if (!_preset.IsGlobal) {
                    Service.Configuration.HookPresets.DefaultPreset.ExtraCfg.Enabled = false;
                }
            }
        }

        if (!_preset.IsGlobal) {
            if (Service.Configuration.HookPresets.DefaultPreset.ExtraCfg.Enabled && !config.Enabled)
                ImGui.TextColored(ImGuiColors.DalamudViolet, UIStrings.Global_Extra_Being_Used);
            else if (!config.Enabled)
                ImGui.TextColored(ImGuiColors.ParsedBlue, UIStrings.SubExtra_Disabled);
        }
        else {
            if (Service.Configuration.HookPresets.SelectedPreset?.ExtraCfg.Enabled ?? false)
                ImGui.TextColored(ImGuiColors.DalamudViolet, string.Format(UIStrings.Custom_Extra_Being_Used, Service.Configuration.HookPresets.SelectedPreset.PresetName));
            else if (!config.Enabled)
                ImGui.TextColored(ImGuiColors.ParsedBlue, UIStrings.SubExtra_Disabled);
        }

        ImGui.Spacing();
    }

    public static void DrawBody(ExtraConfig config) {
        using var item = ImRaii.Child("###ExtraItems", new Vector2(0, 0), true);
        using (ImRaii.Group()) {
            ImGui.TextColored(ImGuiColors.DalamudYellow, UIStrings.BaitPresetPriorityWarning);

            DrawUtil.SpacingSeparator();

            DrawUtil.DrawCheckboxTree(UIStrings.ForceBaitSwap, ref config.ForceBaitSwap,
                () => {
                    DrawUtil.TextV(UIStrings.SelectBaitStartFishing);
                    DrawUtil.DrawComboSelector(GameRes.Baits, bait => $"[#{bait.Id}] {bait.Name}", $"{MultiString.GetItemName(config.ForcedBaitId)}", bait => config.ForcedBaitId = bait.Id);
                }
            );

            DrawUtil.SpacingSeparator();

            DrawAutoOceanFish(config);

            DrawUtil.SpacingSeparator();

            DrawTriggers(config);

            DrawUtil.SpacingSeparator();

            DrawUtil.Checkbox(UIStrings.Reset_counter_after_swapping_presets, ref config.ResetCounterPresetSwap);
        }
    }

    private static void DrawAutoOceanFish(ExtraConfig config) {
        if (!Service.Configuration.AutoOceanFish) {
            ImGui.TextColored(ImGuiColors.ParsedGrey, "Enable Auto ocean fishing in Settings to use this.");
            return;
        }

        var enabled = config.AutoOceanFishEnabled;
        if (DrawUtil.DrawCheckboxHeader(UIStrings.UseWithOceanFishing, ref enabled, ImGuiTreeNodeFlags.DefaultOpen, () => {
            if (DrawUtil.Checkbox(UIStrings.UseForAllZoneTimes, ref config.AutoOceanFishAllStops)) {
                Service.Save();
            }

            if (!config.AutoOceanFishAllStops) {
                ImGui.SetNextItemWidth(280.Scaled());
                var stopLabel = config.AutoOceanFishSpotId != 0 && config.AutoOceanFishTimeId != 0
                    ? OceanStopUtil.FormatStopLabel(config.AutoOceanFishSpotId, config.AutoOceanFishTimeId)
                    : UIStrings.SelectZoneAndTime;

                var selected = new OceanStopKey(config.AutoOceanFishSpotId, config.AutoOceanFishTimeId);
                using var combo = ImRaii.Combo($"##ZoneTimeSelector", stopLabel);
                if (combo) {
                    foreach (var stop in OceanStopUtil.GetUniqueStops().OrderBy(s => s.SpotId).ThenBy(s => s.TimeId)) {
                        if (ImGui.Selectable(OceanStopUtil.FormatStopLabel(stop.SpotId, stop.TimeId), stop.SpotId == selected.SpotId && stop.TimeId == selected.TimeId)) {
                            config.AutoOceanFishSpotId = stop.SpotId;
                            config.AutoOceanFishTimeId = stop.TimeId;
                            Service.Save();
                        }
                    }
                }
            }

            config.AutoOceanFishConditionSet = ConditionUi.DrawConditionSetSlim(UIStrings.When, config.AutoOceanFishConditionSet, ConditionScope.Hook, showAdvanced: true);
        })) {
            config.AutoOceanFishEnabled = enabled;
            Service.Save();
        }
    }

    private static void DrawTriggers(ExtraConfig config) {
        ImGui.TextV(ImGuiColors.DalamudYellow, UIStrings.SwapStopRules);

        ImGui.SameLine();
        var newlyAddedIndex = -1;
        if (ImGui.SmallIconButton(FontAwesomeIcon.Plus)) {
            newlyAddedIndex = config.Triggers.Count;
            config.Triggers.Add(new ExtraTrigger {
                ConditionSet = new ConditionSet(),
                SwapPreset = false,
                SwapBait = false,
                StopAction = ExtraStopAction.None,
            });
            Service.Save();
        }
        ImGui.TooltipOnHover(UIStrings.Add);

        for (var i = 0; i < config.Triggers.Count; i++) {
            var trig = config.Triggers[i];
            trig.EnsureUiId();
            using var id = ImRaii.PushId(trig.UiId);

            var headerLabel = GetTriggerHeaderLabel(i, trig);
            var enabled = trig.Enabled;
            var forceOpen = i == newlyAddedIndex;
            var removed = false;

            if (DrawUtil.DrawCheckboxHeader(headerLabel, ref enabled, ImGuiTreeNodeFlags.DefaultOpen, () => {
                trig.ConditionSet = ConditionUi.DrawConditionSetSlim(UIStrings.When, trig.ConditionSet, ConditionScope.Hook, showAdvanced: true, drawHeaderExtras: () => {
                    ImGui.SameLine(0, 3.Scaled());
                    if (ImGuiComponents.IconButton(FontAwesomeIcon.Trash)) {
                        config.Triggers.RemoveAt(i);
                        Service.Save();
                        removed = true;
                    }
                    ImGui.TooltipOnHover(UIStrings.Delete);
                });

                if (removed)
                    return;

                ImGui.Separator();
                ImGui.Indent(20.Scaled());
                var startFishing = trig.StartFishing;
                DrawUtil.DrawCheckboxTree("Start fishing", ref startFishing, null);
                trig.StartFishing = startFishing;

                var reduceFish = trig.ReduceFish;
                DrawUtil.DrawCheckboxTree(UIStrings.AetherialReduction_ReduceFish, ref reduceFish, null,
                    UIStrings.AetherialReduction_ReduceFishHelp);
                trig.ReduceFish = reduceFish;

                var stopEnabled = trig.StopAction != ExtraStopAction.None;
                DrawUtil.DrawCheckboxTree(UIStrings.StopQuitFishing, ref stopEnabled,
                    () => {
                        if (ImGui.RadioButton(UIStrings.Stop_Casting, trig.StopAction == ExtraStopAction.StopOnly)) {
                            trig.StopAction = ExtraStopAction.StopOnly;
                            Service.Save();
                        }

                        ImGui.SameLine();
                        ImGuiComponents.HelpMarker(UIStrings.Auto_Cast_Stopped);

                        if (ImGui.RadioButton(UIStrings.Quit_Fishing, trig.StopAction == ExtraStopAction.QuitFishing)) {
                            trig.StopAction = ExtraStopAction.QuitFishing;
                            Service.Save();
                        }
                    });

                if (!stopEnabled && trig.StopAction != ExtraStopAction.None) {
                    trig.StopAction = ExtraStopAction.None;
                    Service.Save();
                }
                else if (stopEnabled && trig.StopAction == ExtraStopAction.None) {
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

                var resolve = trig.ResolveCollectablesWindow;
                var forceNo = trig.ResolveCollectablesForceNo;
                DrawUtil.DrawCheckboxTree("Resolve collectables window", ref resolve, () => DrawUtil.Checkbox("Force NO", ref forceNo));
                trig.ResolveCollectablesWindow = resolve;
                trig.ResolveCollectablesForceNo = forceNo;

                var removeStatus = trig.RemoveStatus;
                var statusToRemove = trig.StatusToRemove;
                DrawRemoveStatus(ref removeStatus, ref statusToRemove);
                if (removeStatus && statusToRemove == 0 && GameRes.FishingStatuses.Count > 0)
                    statusToRemove = GameRes.FishingStatuses[0];
                trig.RemoveStatus = removeStatus;
                trig.StatusToRemove = statusToRemove;

                trig.NotifyOnSuccess.DrawConfig(string.Empty);

                ImGui.Unindent(20.Scaled());
            }, helpText: string.Empty, forceOpen: forceOpen)) {
                trig.Enabled = enabled;
                Service.Save();
            }

            if (removed) {
                i--;
                continue;
            }
        }
    }

    private static string GetTriggerHeaderLabel(int index, ExtraTrigger trig) {
        var summary = SummarizeTrigger(trig);
        return string.IsNullOrEmpty(summary)
            ? $"Rule {index + 1}"
            : $"Rule {index + 1} – {summary}";
    }

    private static string SummarizeTrigger(ExtraTrigger trig) {
        if (trig.ReduceFish)
            return UIStrings.AetherialReduction_ReduceFish;

        if (trig.RemoveStatus && trig.StatusToRemove != 0)
            return $"Remove {MultiString.GetStatusName(trig.StatusToRemove)}";

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
        if (hasInv) {
            if (invObj is bool b) inv = b;
            else if (invObj is long l) inv = l != 0;
        }

        var prefix = inv ? "OnLose " : "OnGain ";

        return prefix + core;
    }

    private static string SummarizeCondition(Condition cond) {
        switch (cond.TypeId) {
            case "IntuitionActive":
                return "Fisher's Intuition";
            case "SpectralActive":
                return "Spectral current";
            case "StatusStacks": {
                    if (cond.Params.TryGetValue("ids", out var idsObj) && idsObj is List<object> list && list.Count == 1) {
                        var id = Convert.ToUInt32(list[0]);
                        if (id == IDs.Status.AnglersArt) {
                            var stacks = 1;
                            if (cond.Params.TryGetValue("minStacks", out var msObj))
                                stacks = Convert.ToInt32(msObj);
                            return $"Angler's Art ≥ {stacks} stacks";
                        }
                    }
                    return "Status stacks";
                }
            case "SwimbaitCount": {
                    var v = 0;
                    if (cond.Params.TryGetValue("val", out var vObj))
                        v = Convert.ToInt32(vObj);
                    var op = cond.Params.TryGetValue("op", out var opObj) ? opObj?.ToString() ?? ">=" : ">=";
                    if (!cond.Params.ContainsKey("op") && cond.Params.TryGetValue("above", out var aObj)) {
                        var above = aObj is bool b ? b : Convert.ToInt32(aObj) != 0;
                        op = above ? ">=" : "<=";
                    }
                    var cmp = op switch {
                        ">" => ">",
                        "<" => "<",
                        "<=" => "≤",
                        "=" => "=",
                        _ => "≥",
                    };
                    var fishId = 0;
                    if (cond.Params.TryGetValue("id", out var idObj))
                        fishId = Convert.ToInt32(idObj);
                    var fishLabel = fishId == 0 ? "slot fish" : MultiString.GetItemName(fishId);
                    return $"Swimbaits ({fishLabel}) {cmp} {v}";
                }
            default:
                return string.Empty;
        }
    }

    private static void DrawPresetSwap(ref bool enable, ref string presetName) {
        using var _ = ImRaii.PushId(@$"{nameof(DrawPresetSwap)}");

        var text = presetName;
        DrawUtil.DrawCheckboxTree(UIStrings.Swap_Preset, ref enable,
            () => DrawUtil.DrawPresetSwapSelector(text, preset => text = preset));

        presetName = text;
    }

    private static void DrawRemoveStatus(ref bool enable, ref uint statusId) {
        using var _ = ImRaii.PushId(@$"{nameof(DrawRemoveStatus)}");

        var selectedId = statusId;
        DrawUtil.DrawCheckboxTree("Remove status", ref enable,
            () => {
                if (GameRes.FishingStatuses.Count == 0)
                    return;

                if (selectedId == 0 || GameRes.FishingStatuses.All(s => s != selectedId))
                    selectedId = GameRes.FishingStatuses[0];

                var selectedLabel = $"{selectedId}: {MultiString.GetStatusName(selectedId)}";
                DrawUtil.DrawComboSelector(GameRes.FishingStatuses, s => $"{s}: {MultiString.GetStatusName(s)}", selectedLabel, s => selectedId = s);
            });

        statusId = selectedId;
    }

    private static void DrawBaitSwap(ref bool enable, ref BaitFishClass baitSwap) {
        using var _ = ImRaii.PushId(@$"{nameof(DrawBaitSwap)}");

        var newBait = baitSwap;
        DrawUtil.DrawCheckboxTree(UIStrings.Swap_Bait, ref enable,
            () => DrawUtil.DrawBaitSwapSelector(newBait, bait => newBait = bait));

        baitSwap = newBait;
    }
}
