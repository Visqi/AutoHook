using AutoHook.Conditions;
using AutoHook.IPC;
using ECommons.Throttlers;
using FFXIVClientStructs.FFXIV.Client.Game.Event;

namespace AutoHook.Fishing;

public partial class FishingManager {
    public ExtraConfig GetExtraCfg()
        => Presets.SelectedPreset?.ExtraCfg.Enabled ?? false
            ? Presets.SelectedPreset.ExtraCfg
            : Presets.DefaultPreset.ExtraCfg;

    private void CheckExtraActions(ExtraConfig extraCfg) {
        if (extraCfg.Triggers.Count > 0)
            RunExtraTriggers(extraCfg);
    }

    private void RunExtraTriggers(ExtraConfig extraCfg) {
        for (var i = 0; i < extraCfg.Triggers.Count; i++) {
            if (extraCfg.Triggers[i] is not { Enabled: true, ConditionSet: not null } trig)
                continue;

            var current = trig.ConditionSet.Evaluate(Ws, ConditionRegistry.Registry);
            var last = i < extraCfg.LastTriggerStates.Count ? extraCfg.LastTriggerStates[i] : false;

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
            var preset = Presets.CustomPresets
                .FirstOrDefault(p => p.PresetName == trig.PresetToSwap);

            Ws.Execute(new WorldState.OpOrFishingStep(FishingSteps.PresetSwapped));

            if (preset != null) {
                Service.Save();
                Presets.SelectedPreset = preset;
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
            Ws.Execute(new WorldState.OpOrFishingStep(FishingSteps.BaitSwapped));

            if (result == ChangeBaitReturn.Success) {
                Service.PrintChat(@$"[Extra] Trigger: Swapping bait to {trig.BaitToSwap.Name}");
                Service.Save();
            }
        }

        if (trig.ResolveCollectablesWindow) {
            Service.AutoCollectables.ResolvePending(trig.ResolveCollectablesForceNo);
        }

        if (trig.StartFishing
            && Ws.Fishing.FishingState is FishingState.None or FishingState.PoleReady
            && Ws.IsCastAvailable()
            && EzThrottler.Throttle("ExtraStartFishingRule", 1000)) {
            StartFishing();
        }

        Service.NotificationMaster.Notify(trig.NotifyOnSuccess, "Rule condition success");
    }
}
