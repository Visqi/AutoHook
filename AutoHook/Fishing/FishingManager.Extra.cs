using AutoHook.Conditions;
using AutoHook.Tasks;
using ECommons.Throttlers;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Event;

namespace AutoHook.Fishing;

public partial class FishingManager {
    public ExtraConfig GetExtraCfg()
        => Presets.SelectedPreset?.ExtraCfg.Enabled ?? false
            ? Presets.SelectedPreset.ExtraCfg
            : Presets.DefaultPreset.ExtraCfg;

    /// <summary>
    /// When <see cref="Configuration.AutoOceanFish"/> is on, select the first preset whose Extra config
    /// is enabled for auto ocean fishing and matches the current zone (spot) and time of day.
    /// </summary>
    private void TryApplyOceanFishingPreset() {
        if (!Service.Configuration.AutoOceanFish)
            return;

        var ocean = Ws.OceanFishing;
        if (ocean == OceanFishingState.Empty || ocean.TimeOfDay == TimeOfDay.None)
            return;

        CustomPresetConfig? match = null;
        foreach (var preset in EnumerateHookPresets()) {
            var extra = preset.ExtraCfg;
            if (!extra.AutoOceanFishEnabled)
                continue;
            if (!extra.AutoOceanFishAllStops
                && !OceanStopUtil.MatchesStop(extra.AutoOceanFishSpotId, extra.AutoOceanFishTimeId, ocean))
                continue;
            if (extra.AutoOceanFishConditionSet is { Groups.Count: > 0 } set
                && set.Groups.Any(g => g.Conditions.Count > 0)
                && !set.Evaluate(Ws, ConditionRegistry.Registry))
                continue;
            match = preset;
            break;
        }

        if (match == null)
            return;

        if (match.IsGlobal) {
            if (Presets.SelectedPreset == null)
                return;
            Presets.SelectedPreset = null;
            Service.PrintDebug($"[AutoOceanFish] Preset set to global (zone {ocean.CurrentZone}, spot {ocean.CurrentSpotId}, time {ocean.CurrentTimeId})");
            return;
        }

        if (Presets.SelectedPreset?.UniqueId == match.UniqueId)
            return;

        Presets.SelectedPreset = match;
        Service.PrintDebug($"[AutoOceanFish] Preset set to {match.PresetName} (zone {ocean.CurrentZone}, spot {ocean.CurrentSpotId}, time {ocean.CurrentTimeId})");
    }

    private IEnumerable<CustomPresetConfig> EnumerateHookPresets() {
        yield return Presets.DefaultPreset;
        foreach (var preset in Presets.CustomPresets)
            yield return preset;
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

        while (true) {
            Ws.Execute(new WorldState.OpClearFishingStepFlag(FishingSteps.PresetSwapped));

            var presetBefore = Presets.SelectedPreset?.UniqueId;
            var extraCfg = GetExtraCfg();
            if (extraCfg.Triggers.Count == 0)
                break;

            RunExtraTriggers(extraCfg);

            if (Presets.SelectedPreset?.UniqueId == presetBefore)
                break;

            anyPresetSwapped = true;
        }

        if (anyPresetSwapped)
                    Ws.Execute(new WorldState.OpSetFishingStep(FishingSteps.PresetSwapped, Or: true));
    }

    private void RunExtraTriggers(ExtraConfig extraCfg) {
        for (var i = 0; i < extraCfg.Triggers.Count; i++) {
            if (extraCfg.Triggers[i] is not { Enabled: true, ConditionSet: not null } trig)
                continue;

            var current = trig.ConditionSet.Evaluate(Ws, ConditionRegistry.Registry);
            var last = i < extraCfg.LastTriggerStates.Count && extraCfg.LastTriggerStates[i];
            var fire = !last && current;

            if (i < extraCfg.LastTriggerStates.Count)
                extraCfg.LastTriggerStates[i] = current;
            else
                extraCfg.LastTriggerStates.Add(current);
            if (!fire)
                continue;

            ExecuteExtraTriggerActions(extraCfg, trig);
        }
    }

    private void ExecuteExtraTriggerActions(ExtraConfig extraCfg, ExtraTrigger trig) {
        // Stop/quit fishing
        if (trig.StopAction == ExtraStopAction.StopOnly) {
            Ws.Execute(new WorldState.OpSetFishingStep(FishingSteps.None));
        }
        else if (trig.StopAction == ExtraStopAction.QuitFishing) {
            Ws.Execute(new WorldState.OpSetFishingStep(FishingSteps.Quitting));
        }

        // Swap preset
        if (trig.SwapPreset && !Ws.FishingStep.HasFlag(FishingSteps.PresetSwapped)) {
            var preset = Presets.CustomPresets.FirstOrDefault(p => p.PresetName == trig.PresetToSwap);

                    Ws.Execute(new WorldState.OpSetFishingStep(FishingSteps.PresetSwapped, Or: true));

            if (preset != null) {
                Service.Save();
                Presets.SelectedPreset = preset;
                preset.ExtraCfg.LastTriggerStates.Clear();
                Service.PrintChat(@$"[Extra] Trigger: Swapping preset to {trig.PresetToSwap}");
                Service.Save();
            }
            else if (!string.IsNullOrEmpty(trig.PresetToSwap) && trig.PresetToSwap != @"-") {
                Service.PrintChat(@$"[Extra] Trigger: Preset {trig.PresetToSwap} not found.");
            }
        }

        // Swap bait
        if (trig.SwapBait && !Ws.FishingStep.HasFlag(FishingSteps.BaitSwapped)) {
            var result = ChangeBait(trig.BaitToSwap);
            Ws.Execute(new WorldState.OpSetFishingStep(FishingSteps.BaitSwapped, Or: true));

            if (result == ChangeBaitReturn.Success) {
                Service.PrintChat(@$"[Extra] Trigger: Swapping bait to {trig.BaitToSwap.Name}");
                Service.Save();
            }
        }

        if (trig.RemoveStatus && trig.StatusToRemove != 0 && Ws.HasStatus(trig.StatusToRemove) && EzThrottler.Throttle("ExtraRemoveStatus", 500)) {
            if (StatusManager.ExecuteStatusOff(trig.StatusToRemove)) {
                Service.PrintChat(@$"[Extra] Trigger: Removed {MultiString.GetStatusName(trig.StatusToRemove)}");
            }
        }

        if (trig.StartFishing && !ShouldSuppressAutoStartFishing() && Ws.Fishing.FishingState is FishingState.None or FishingState.PoleReady && Ws.IsCastAvailable() && EzThrottler.Throttle("ExtraStartFishingRule", 1000)) {
            StartFishing();
        }

        if (trig.ReduceFish && Svc.Automation.CurrentTask is not AetherialReduction) {
            Svc.Automation.Start(new AetherialReduction(this));
            Service.PrintChat(UIStrings.AetherialReduction_Started);
        }

        Service.NotificationMaster.TryNotify(trig.NotifyOnSuccess with { ToastText = "Rule condition success" });
    }
}
