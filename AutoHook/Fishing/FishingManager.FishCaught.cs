using AutoHook.Conditions;

namespace AutoHook.Fishing;

public partial class FishingManager {
    private FishConfig? GetLastCatchConfig() {
        if (Ws.LastCaughtFishId == null)
            return null;

        return Presets.SelectedPreset?.GetFishById(Ws.LastCaughtFishId.Value) ?? Presets.DefaultPreset.GetFishById(Ws.LastCaughtFishId.Value);
    }

    private bool UseFishCaughtActions(FishConfig? lastFishCatchCfg) {
        BaseActionCast? cast = null;

        if (lastFishCatchCfg == null || !lastFishCatchCfg.Enabled || Ws.FishingStep.HasFlag(FishingSteps.PresetSwapped))
            return false;

        // Ignore logic is entirely driven by the condition set; legacy bools are used only for migration.
        if (lastFishCatchCfg.IgnoreConditionSet is { Groups.Count: > 0 } &&
            lastFishCatchCfg.IgnoreConditionSet.Evaluate(Service.WorldState, ConditionRegistry.Registry))
            return false;

        var caughtCount = FishingHelper.GetFishCount(lastFishCatchCfg.UniqueId);

        if (Ws.LastCaughtFishId != null)
            lastFishCatchCfg.SparefulHand.FishIdToCheck = (uint)Ws.LastCaughtFishId.Value;

        if (lastFishCatchCfg.IdenticalCast.IsAvailableToCast(caughtCount))
            cast = lastFishCatchCfg.IdenticalCast;

        // global IC
        if (cast == null) {
            var autoCastCfg = GetAutoCastCfg();
            if (autoCastCfg.CastIdenticalCast.IsAvailableToCast(caughtCount))
                cast = autoCastCfg.CastIdenticalCast;
        }

        if (lastFishCatchCfg.SurfaceSlap.IsAvailableToCast())
            cast = lastFishCatchCfg.SurfaceSlap;

        if (lastFishCatchCfg.SparefulHand.IsAvailableToCast())
            cast = lastFishCatchCfg.SparefulHand;

        var multiHook = lastFishCatchCfg.Multihook;

        if (cast == null && multiHook.Enabled && multiHook.CastCondition()) {
            Service.TaskManager.Enqueue(() =>
                PlayerRes.CastActionDelayed(multiHook.Id, multiHook.ActionType, multiHook.GetName()));
            Service.TaskManager.Enqueue(() =>
                CastLineMoochOrRelease(GetAutoCastCfg(), lastFishCatchCfg));
            return true;
        }

        if (cast != null) {
            if (multiHook.Enabled && multiHook.CastCondition()) {
                Service.TaskManager.Enqueue(() =>
                    PlayerRes.CastActionDelayed(multiHook.Id, multiHook.ActionType, multiHook.GetName()));
                Service.TaskManager.Enqueue(() =>
                    PlayerRes.CastActionDelayed(cast.Id, cast.ActionType, cast.Name));
                return true;
            }

            PlayerRes.CastActionDelayed(cast.Id, cast.ActionType, cast.Name);
            return true;
        }

        return false;
    }

    private void CheckFishCaughtSwap(FishConfig? lastCatchCfg) {
        if (lastCatchCfg == null || !lastCatchCfg.Enabled)
            return;

        var guid = lastCatchCfg.UniqueId;

        if (lastCatchCfg.SwapPresetLimit.BackingSet is { Groups.Count: > 0 } && Presets.SelectedPreset?.PresetName == lastCatchCfg.PresetToSwap)
            FishingHelper.RemovePresetSwap(guid);

        if (lastCatchCfg.SwapPresetLimit.BackingSet is { Groups.Count: > 0 } presetSet &&
            !FishingHelper.SwappedPreset(guid) && !Ws.FishingStep.HasFlag(FishingSteps.PresetSwapped)) {
            var readyForPresetSwap = presetSet.Evaluate(Ws, ConditionRegistry.Registry);
            if (readyForPresetSwap && lastCatchCfg.PresetToSwap != Presets.SelectedPreset?.PresetName) {
                var preset = Presets.CustomPresets.FirstOrDefault(preset => preset.PresetName == lastCatchCfg.PresetToSwap);

                FishingHelper.AddPresetSwap(guid);
                Ws.Execute(new WorldState.OpOrFishingStep(FishingSteps.PresetSwapped));

                if (preset == null)
                    Service.PrintChat(@$"Preset {lastCatchCfg.PresetToSwap} not found.");
                else {
                    Service.Save();
                    Presets.SelectedPreset = preset;
                    Service.PrintChat(@$"[Fish Caught] Swapping current preset to {lastCatchCfg.PresetToSwap}");
                    Service.Save();
                }
            }
        }

        if (lastCatchCfg.SwapBaitLimit.BackingSet is { Groups.Count: > 0 } baitSet &&
            !FishingHelper.SwappedBait(guid) && !Ws.FishingStep.HasFlag(FishingSteps.BaitSwapped)) {
            var readyForBaitSwap = baitSet.Evaluate(Ws, ConditionRegistry.Registry);
            if (readyForBaitSwap &&
                lastCatchCfg.BaitToSwap.Id != Ws.CurrentBaitId) {
                var result = Service.BaitManager.ChangeBait(lastCatchCfg.BaitToSwap);

                FishingHelper.AddBaitSwap(guid);
                Ws.Execute(new WorldState.OpOrFishingStep(FishingSteps.BaitSwapped));
                if (result == BaitManager.ChangeBaitReturn.Success) {
                    Service.PrintChat(@$"[Fish Caught] Swapping bait to {lastCatchCfg.BaitToSwap.Name}");
                    Service.Save();
                }
                if (lastCatchCfg.SwapBaitResetCount) FishingHelper.ToBeRemoved.Add(guid);
            }
        }
    }
}
