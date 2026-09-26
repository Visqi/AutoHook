using AutoHook.Conditions;
using AutoHook.Tasks;
using ECommons.Throttlers;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Event;
using StatusSheet = Lumina.Excel.Sheets.Status;

namespace AutoHook.Fishing;

public partial class FishingManager {
    private const int _presetSwapCap = 8;

    public ExtraConfig GetExtraCfg()
        => Presets.SelectedPreset?.ExtraCfg.Enabled ?? false ? Presets.SelectedPreset.ExtraCfg : Presets.DefaultPreset.ExtraCfg;

    private void TryApplyOceanFishingPreset() {
        if (!Service.Configuration.AutoOceanFish)
            return;

        var ocean = Ws.OceanFishing;
        if (ocean == OceanFishingState.Empty || ocean.TimeOfDay == TimeOfDay.None)
            return;

        OceanGoalCatalog.PrefetchRouteAchievements(ocean.CurrentRoute);

        var settingsGoal = Service.Configuration.AutoOceanFishGoal;
        var stop = OceanStopUtil.FormatStopLabel(ocean.CurrentSpotId, ocean.CurrentTimeId);
        var fallthrough = Service.Configuration.AOF_Fallthrough ? "fallthrough if acquired" : "keep goal if acquired";
        var about = $"{settingsGoal} · route {ocean.CurrentRoute} · zone {ocean.CurrentZone + 1} · {stop} · {fallthrough}";
        var skips = new List<string>();

        foreach (var tier in OceanGoalCatalog.GetCascade(settingsGoal)) {
            if (tier == OceanFishGoalKind.Achievement) {
                if (!TryMatchAchievementTier(ocean, skips, out var achPreset, out _))
                    continue;
                ApplyOceanPresetChoice(about, skips, achPreset, OceanFishGoalKind.Achievement);
                return;
            }

            if (tier == OceanFishGoalKind.Legendary) {
                if (!TryMatchLegendaryTier(ocean, skips, out var legPreset))
                    continue;
                ApplyOceanPresetChoice(about, skips, legPreset, OceanFishGoalKind.Legendary);
                return;
            }

            if (tier == OceanFishGoalKind.Levelling) {
                if (!TryMatchLevellingTier(ocean, skips, out var levPreset))
                    continue;
                ApplyOceanPresetChoice(about, skips, levPreset, OceanFishGoalKind.Levelling);
                return;
            }

            // Points (or other residual tier)
            var pointsPreset = FindOceanPresetForGoal(ocean, tier, goalId: null);
            if (pointsPreset == null) {
                skips.Add($"{tier} — no matching preset");
                continue;
            }

            ApplyOceanPresetChoice(about, skips, pointsPreset, tier);
            return;
        }

        Ws.Decide(DecisionContext.OceanPreset, false, "No matching preset", JoinOceanDetail(about, skips));
    }

    private bool TryMatchAchievementTier(OceanFishingState ocean, List<string> skips, out CustomPresetConfig preset, out uint achievementId) {
        preset = null!;
        achievementId = 0;

        var forRoute = OceanGoalCatalog.GetAchievementsForRoute(ocean.CurrentRoute).ToList();
        if (forRoute.Count == 0) {
            skips.Add("Achievement — none on this route");
            return false;
        }

        var partySize = Math.Max(1, Ws.Party.QueuedWithContentIds.Count);
        var statusParts = forRoute.Select(def => {
            if (partySize < def.MinPartySize)
                return $"#{def.AchievementId} party<{def.MinPartySize}";
            return OceanGoalCatalog.IsAchievementIncomplete(def.AchievementId) switch {
                true => $"#{def.AchievementId} incomplete",
                false => $"#{def.AchievementId} obtained",
                null => $"#{def.AchievementId} unknown",
            };
        });
        var status = string.Join(", ", statusParts);

        var skipIfAcquired = Service.Configuration.AOF_Fallthrough;
        var eligible = OceanGoalCatalog.GetEligibleAchievementIds(ocean.CurrentRoute, skipIfAcquired);
        if (eligible.Count == 0) {
            skips.Add(skipIfAcquired
                ? $"Achievement — not eligible ({status})"
                : $"Achievement — not eligible, party size ({status})");
            return false;
        }

        foreach (var achId in eligible) {
            var match = FindOceanPresetForGoal(ocean, OceanFishGoalKind.Achievement, achId);
            if (match == null)
                continue;
            preset = match;
            achievementId = achId;
            return true;
        }

        skips.Add($"Achievement — no matching preset (eligible {string.Join(",", eligible)}; {status})");
        return false;
    }

    private bool TryMatchLevellingTier(OceanFishingState ocean, List<string> skips, out CustomPresetConfig preset) {
        preset = null!;

        if (!OceanGoalCatalog.IsLevellingNeeded()) {
            if (Service.Configuration.AOF_Fallthrough) {
                skips.Add("Levelling — max level");
                return false;
            }
        }

        var match = FindOceanPresetForGoal(ocean, OceanFishGoalKind.Levelling, goalId: null);
        if (match == null) {
            skips.Add("Levelling — no matching preset");
            return false;
        }

        preset = match;
        return true;
    }

    private bool TryMatchLegendaryTier(OceanFishingState ocean, List<string> skips, out CustomPresetConfig preset) {
        preset = null!;

        var forRoute = OceanGoalCatalog.GetLegendariesForRoute(ocean.CurrentRoute).ToList();
        if (forRoute.Count == 0) {
            skips.Add("Legendary — none on this route");
            return false;
        }

        var status = string.Join(", ", forRoute.Select(f =>
            $"#{f.FishParameterId} {(OceanGoalCatalog.IsLegendaryCaught(f.FishParameterId) ? "caught" : "uncaught")}"));

        var skipIfAcquired = Service.Configuration.AOF_Fallthrough;
        var eligible = OceanGoalCatalog.GetEligibleLegendaryIds(ocean.CurrentRoute, skipIfAcquired);
        if (eligible.Count == 0) {
            skips.Add($"Legendary — already caught ({status})");
            return false;
        }

        var match = FindOceanPresetForGoal(ocean, OceanFishGoalKind.Legendary, goalId: null);
        if (match == null) {
            skips.Add($"Legendary — no matching preset (still need {string.Join(",", eligible)}; {status})");
            return false;
        }

        preset = match;
        return true;
    }

    private void ApplyOceanPresetChoice(string about, List<string> skips, CustomPresetConfig match, OceanFishGoalKind tier) {
        if (match.IsGlobal) {
            var alreadyGlobal = Presets.SelectedPreset == null;
            if (!alreadyGlobal)
                Presets.Select(null, FishingPresets.ReasonAutoOceanFish);
            Ws.Decide(DecisionContext.OceanPreset, true,
                alreadyGlobal ? $"Already on global ({tier})" : $"Selected global ({tier})",
                JoinOceanDetail(about, skips), Service.GlobalPresetName);
            return;
        }

        var alreadySelected = Presets.SelectedPreset?.UniqueId == match.UniqueId;
        if (!alreadySelected)
            Presets.Select(match, FishingPresets.ReasonAutoOceanFish);

        Ws.Decide(DecisionContext.OceanPreset, true,
            alreadySelected ? $"Already on {match.PresetName} ({tier})" : $"Selected {match.PresetName} ({tier})",
            JoinOceanDetail(about, skips), match.PresetName);
    }

    private static string JoinOceanDetail(string about, List<string> skips) {
        if (skips.Count == 0)
            return about;
        return about + "\n" + string.Join("\n", skips);
    }

    private CustomPresetConfig? FindOceanPresetForGoal(OceanFishingState ocean, OceanFishGoalKind tier, uint? goalId) {
        foreach (var preset in EnumerateHookPresets()) {
            if (!MatchesOceanBase(preset.ExtraCfg, ocean))
                continue;
            if (preset.ExtraCfg.AutoOceanFishGoal != tier)
                continue;
            if (goalId is { } id && preset.ExtraCfg.AutoOceanFishGoalId != id)
                continue;
            return preset;
        }

        return null;
    }

    private static bool MatchesOceanBase(ExtraConfig extra, OceanFishingState ocean) {
        if (!extra.AutoOceanFishEnabled)
            return false;
        if (!extra.AutoOceanFishAllStops && !OceanStopUtil.MatchesStop(extra.AutoOceanFishSpotId, extra.AutoOceanFishTimeId, ocean))
            return false;
        return !(extra.AutoOceanFishConditionSet is { } set) || !set.HasAnyCondition() || !set.Fails();
    }

    private IEnumerable<CustomPresetConfig> EnumerateHookPresets() {
        yield return Presets.DefaultPreset;
        foreach (var preset in Presets.CustomPresets)
            yield return preset;
    }

    private CustomPresetConfig? FindPresetByName(string presetName) {
        if (string.IsNullOrEmpty(presetName) || presetName == @"-")
            return null;

        if (Presets.DefaultPreset.PresetName == presetName)
            return Presets.DefaultPreset;

        return Presets.CustomPresets.FirstOrDefault(p => p.PresetName == presetName);
    }

    private CustomPresetConfig GetExtraOwnerPreset()
        => Presets.SelectedPreset?.ExtraCfg.Enabled == true ? Presets.SelectedPreset : Presets.DefaultPreset;

    private bool ExtraSwapStillNeeded(ExtraTrigger trig) {
        if (trig.SwapPreset && !string.IsNullOrEmpty(trig.PresetToSwap) && trig.PresetToSwap != @"-" && Presets.SelectedPreset?.PresetName != trig.PresetToSwap)
            return true;
        return trig.SwapBait && trig.BaitToSwap.Id > 0 && Ws.Fishing.BaitInfo.BaitId != trig.BaitToSwap.Id;
    }

    private void QueueResolveCollectables() {
        var extraCfg = GetExtraCfg();
        foreach (var trig in extraCfg.Triggers) {
            if (trig is not { Enabled: true, ResolveCollectablesWindow: true, ConditionSet: not null })
                continue;

            if (!trig.ConditionSet.Evaluate(Ws, ConditionRegistry.Registry))
                continue;

            Service.AutoCollectables.RequestResolve(trig.ResolveCollectablesForceNo);
            return;
        }
    }

    private void CheckExtraActions() {
        var anyPresetSwapped = false;
        var involvedPresetIds = new HashSet<Guid>();
        var iterations = 0;

        try {
            while (true) {
                if (++iterations > _presetSwapCap) {
                    SwapLoopBailout(involvedPresetIds);
                    break;
                }

                Ws.Execute(new FishingInfo.OpClearFishingStepFlag(FishingSteps.PresetSwapped));
                Ws.Execute(new FishingInfo.OpClearFishingStepFlag(FishingSteps.BaitSwapped));

                involvedPresetIds.Add(GetExtraOwnerPreset().UniqueId);

                var presetBefore = Presets.SelectedPreset?.UniqueId;
                var extraCfg = GetExtraCfg();
                if (extraCfg.Triggers.Count == 0)
                    break;

                RunExtraTriggers(extraCfg);

                if (Presets.SelectedPreset?.UniqueId == presetBefore)
                    break;

                anyPresetSwapped = true;
                involvedPresetIds.Add(GetExtraOwnerPreset().UniqueId);
            }

            if (anyPresetSwapped)
                Ws.Execute(new FishingInfo.OpSetFishingStep(FishingSteps.PresetSwapped, Or: true));
        }
        finally {
            SettleIntuitionEdges();
        }
    }

    // turned gained/lost into active/inactive
    private void SettleIntuitionEdges() {
        var cur = Ws.Fishing.Intuition;
        if (cur.Status == IntuitionStatus.Gained)
            Ws.Execute(new FishingInfo.OpIntuition(new IntuitionInfo(IntuitionStatus.Active, cur.TimeRemaining)));
        else if (cur.Status == IntuitionStatus.Lost)
            Ws.Execute(new FishingInfo.OpIntuition(new IntuitionInfo(IntuitionStatus.NotActive, 0f)));
    }

    private void SwapLoopBailout(HashSet<Guid> involvedPresetIds) {
        var involvedNames = EnumerateHookPresets().Where(p => involvedPresetIds.Contains(p.UniqueId)).Select(p => p.PresetName).ToList();

        Service.Configuration.PluginEnabled = false;
        Service.Save();

        var presetList = involvedNames.Count > 0 ? string.Join(", ", involvedNames) : UIStrings.UnknownPresets;
        Service.PrintChat(string.Format(UIStrings.Extra_PresetSwapLoop_Bailout, presetList));
    }

    private void RunExtraTriggers(ExtraConfig extraCfg) {
        for (var i = 0; i < extraCfg.Triggers.Count; i++) {
            if (extraCfg.Triggers[i] is not { Enabled: true, ConditionSet: not null } trig)
                continue;

            var current = trig.ConditionSet.Evaluate(Ws, ConditionRegistry.Registry);
            var last = i < extraCfg.LastTriggerStates.Count && extraCfg.LastTriggerStates[i];
            var fire = current && (!last || ExtraSwapStillNeeded(trig));

            if (i < extraCfg.LastTriggerStates.Count)
                extraCfg.LastTriggerStates[i] = current;
            else
                extraCfg.LastTriggerStates.Add(current);
            if (!fire)
                continue;

            Ws.Decide(DecisionContext.Extra, true, trig.GetRuleLabel(i), JoinDetail(trig.DescribeActions(), trig.ConditionSet.Describe()), GetExtraOwnerPreset().PresetName);
            ExecuteExtraTriggerActions(extraCfg, trig);
        }
    }

    private static string? JoinDetail(params string?[] parts) {
        var joined = string.Join("\n", parts.Where(p => !string.IsNullOrEmpty(p)));
        return string.IsNullOrEmpty(joined) ? null : joined;
    }

    private void ExecuteExtraTriggerActions(ExtraConfig extraCfg, ExtraTrigger trig) {
        // Stop/quit fishing
        if (trig.StopAction == ExtraStopAction.StopOnly) {
            Ws.Execute(new FishingInfo.OpSetFishingStep(FishingSteps.None));
        }
        else if (trig.StopAction == ExtraStopAction.QuitFishing) {
            Ws.Execute(new FishingInfo.OpSetFishingStep(FishingSteps.Quitting));
        }

        if (trig.ResetFishCaughtCounter) {
            GetExtraOwnerPreset().ResetCounter();
            Service.PrintChat(@$"[Extra] Trigger: Reset fish caught counter");
        }

        // Swap preset
        if (trig.SwapPreset && !Ws.Fishing.FishingStep.HasFlag(FishingSteps.PresetSwapped)) {
            if (Presets.CurrentPreset.PresetName == trig.PresetToSwap) {
                Ws.Execute(new FishingInfo.OpSetFishingStep(FishingSteps.PresetSwapped, Or: true));
                FindPresetByName(trig.PresetToSwap)?.TryResetCounter();
            }
            else {
                var preset = FindPresetByName(trig.PresetToSwap);

                Ws.Execute(new FishingInfo.OpSetFishingStep(FishingSteps.PresetSwapped, Or: true));

                if (preset != null) {
                    Service.Save();
                    Presets.Select(preset, FishingPresets.ReasonExtraTrigger);
                    preset.ExtraCfg.LastTriggerStates.Clear();
                    Service.PrintChat(@$"[Extra] Trigger: Swapping preset to {trig.PresetToSwap}");
                    Service.Save();
                }
                else if (!string.IsNullOrEmpty(trig.PresetToSwap) && trig.PresetToSwap != @"-") {
                    Service.PrintChat(@$"[Extra] Trigger: Preset {trig.PresetToSwap} not found.");
                }
            }
        }

        // Swap bait
        if (trig.SwapBait && !Ws.Fishing.FishingStep.HasFlag(FishingSteps.BaitSwapped)) {
            var result = ChangeBait(trig.BaitToSwap);
            Ws.Execute(new FishingInfo.OpSetFishingStep(FishingSteps.BaitSwapped, Or: true));

            if (result is ChangeBaitReturn.Success or ChangeBaitReturn.AlreadyEquipped) {
                Service.PrintChat(@$"[Extra] Trigger: Swapping bait to {trig.BaitToSwap.Name}");
                Service.Save();
            }
        }

        if (trig.RemoveStatus && trig.StatusToRemove != 0 && Ws.Player.HasStatus(trig.StatusToRemove) && EzThrottler.Throttle("ExtraRemoveStatus", 500)) {
            if (StatusManager.ExecuteStatusOff(trig.StatusToRemove)) {
                Service.PrintChat(@$"[Extra] Trigger: Removed {StatusSheet.GetRow(trig.StatusToRemove).Name}");
            }
        }

        if (trig.StartFishing && !ShouldSuppressAutoStartFishing() && Ws.Fishing.FishingState is FishingState.None or FishingState.PoleReady && Ws.IsCastAvailable() && EzThrottler.Throttle("ExtraStartFishingRule", 1000)) {
            StartFishing();
        }

        if (trig.ReduceFish && Svc.Automation.CurrentTask is not AetherialReduction) {
            Svc.Automation.Start(new AetherialReduction(this));
            Service.PrintChat(UIStrings.AetherialReduction_Started);
        }

        Service.NotificationMaster.TryNotify(trig.NotifyOnSuccess, "Rule condition success");
    }
}
