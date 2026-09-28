using AutoHook.Conditions;

namespace AutoHook.Modules.Rod;

public sealed class ExtraComponent(RodFishingModule module) : RodComponent(module) {
    private const int PresetSwapCap = 8;

    public ExtraConfig GetExtraCfg()
        => RodFishingModule.Presets.SelectedPreset?.ExtraCfg.Enabled ?? false ? RodFishingModule.Presets.SelectedPreset.ExtraCfg : RodFishingModule.Presets.DefaultPreset.ExtraCfg;

    public void TryApplyOceanFishingPreset() {
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
            skips.Add(skipIfAcquired ? $"Achievement — not eligible ({status})" : $"Achievement — not eligible, party size ({status})");
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
        var presets = RodFishingModule.Presets;
        if (match.IsGlobal) {
            var alreadyGlobal = presets.SelectedPreset == null;
            if (!alreadyGlobal)
                presets.Select(null, FishingPresets.ReasonAutoOceanFish);
            Ws.Decide(DecisionContext.OceanPreset, true, alreadyGlobal ? $"Already on global ({tier})" : $"Selected global ({tier})", JoinOceanDetail(about, skips), Service.GlobalPresetName);
            return;
        }

        var alreadySelected = presets.SelectedPreset?.UniqueId == match.UniqueId;
        if (!alreadySelected)
            presets.Select(match, FishingPresets.ReasonAutoOceanFish);

        Ws.Decide(DecisionContext.OceanPreset, true, alreadySelected ? $"Already on {match.PresetName} ({tier})" : $"Selected {match.PresetName} ({tier})", JoinOceanDetail(about, skips), match.PresetName);
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
        return !(extra.AutoOceanFishConditionSet is { } set) || !set.HasAnyCondition() || !set.Fails(Service.WorldState);
    }

    private IEnumerable<CustomPresetConfig> EnumerateHookPresets() {
        yield return RodFishingModule.Presets.DefaultPreset;
        foreach (var preset in RodFishingModule.Presets.CustomPresets)
            yield return preset;
    }

    public CustomPresetConfig? FindPresetByName(string presetName) {
        if (string.IsNullOrEmpty(presetName) || presetName == @"-")
            return null;

        var presets = RodFishingModule.Presets;
        if (presets.DefaultPreset.PresetName == presetName)
            return presets.DefaultPreset;

        return presets.CustomPresets.FirstOrDefault(p => p.PresetName == presetName);
    }

    private CustomPresetConfig GetExtraOwnerPreset()
        => RodFishingModule.Presets.SelectedPreset?.ExtraCfg.Enabled == true ? RodFishingModule.Presets.SelectedPreset : RodFishingModule.Presets.DefaultPreset;

    private bool ExtraSwapStillNeeded(ExtraTrigger trig) {
        if (trig.SwapPreset && !string.IsNullOrEmpty(trig.PresetToSwap) && trig.PresetToSwap != @"-" && RodFishingModule.Presets.SelectedPreset?.PresetName != trig.PresetToSwap)
            return true;
        return trig.SwapBait && trig.BaitToSwap.Id > 0 && Ws.Fishing.BaitInfo.BaitId != trig.BaitToSwap.Id;
    }

    public void QueueResolveCollectables() {
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

    public void ProcessExtraActions(ActionHints hints, ActionHintsResolver resolver) {
        var anyPresetSwapped = false;
        var involvedPresetIds = new HashSet<Guid>();
        var iterations = 0;

        try {
            while (true) {
                if (++iterations > PresetSwapCap) {
                    SwapLoopBailout(involvedPresetIds);
                    break;
                }

                Ws.Execute(new FishingInfo.OpClearFishingStepFlag(FishingSteps.PresetSwapped));
                Ws.Execute(new FishingInfo.OpClearFishingStepFlag(FishingSteps.BaitSwapped));

                involvedPresetIds.Add(GetExtraOwnerPreset().UniqueId);

                var extraCfg = GetExtraCfg();
                if (extraCfg.Triggers.Count == 0)
                    break;

                hints.SideEffects.Clear();
                ProposeExtraTriggerSideEffects(hints, extraCfg);

                // apply effects, and if unique id changed, loop re-checking the new extracfg
                var presetChanged = resolver.ApplySideEffects(Ws, hints, Rod);
                if (!presetChanged)
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

    private void ProposeExtraTriggerSideEffects(ActionHints hints, ExtraConfig extraCfg) {
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
            AddExtraTriggerSideEffects(hints, trig);
        }
    }

    private static string? JoinDetail(params string?[] parts) {
        var joined = string.Join("\n", parts.Where(p => !string.IsNullOrEmpty(p)));
        return string.IsNullOrEmpty(joined) ? null : joined;
    }

    private static void AddExtraTriggerSideEffects(ActionHints hints, ExtraTrigger trig) {
        if (trig.StopAction != ExtraStopAction.None)
            hints.AddSideEffect(new StopFishingHint(trig.StopAction));

        if (trig.ResetFishCaughtCounter)
            hints.AddSideEffect(new ResetCounterHint());

        if (trig.SwapPreset)
            hints.AddSideEffect(new SwapPresetHint(trig.PresetToSwap));

        if (trig.SwapBait)
            hints.AddSideEffect(new SwapBaitHint(trig.BaitToSwap));

        if (trig.RemoveStatus && trig.StatusToRemove != 0)
            hints.AddSideEffect(new RemoveStatusHint(trig.StatusToRemove));

        if (trig.StartFishing)
            hints.AddSideEffect(new StartFishingHint());

        if (trig.ReduceFish)
            hints.AddSideEffect(new StartReductionHint());

        hints.AddSideEffect(new NotifyHint(trig.NotifyOnSuccess, "Rule condition success"));
    }
}
