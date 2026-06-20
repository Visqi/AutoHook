using AutoHook.Conditions;

namespace AutoHook.Fishing;

public partial class FishingManager {
    private FishConfig? GetLastCatchConfig() {
        if (Ws.Fishing.LastCatch is not { } lc || lc.FishId <= 0)
            return null;

        return Presets.SelectedPreset?.GetFishById(lc.FishId) ?? Presets.DefaultPreset.GetFishById(lc.FishId);
    }

    private bool UseFishCaughtActions(FishConfig? lastFishCatchCfg) {
        BaseActionCast? cast = null;

        if (lastFishCatchCfg == null || !lastFishCatchCfg.Enabled || Ws.FishingStep.HasFlag(FishingSteps.PresetSwapped))
            return false;

        // Treat an "empty" ignore set (only empty groups, no conditions) as if it wasn't configured at all.
        // Can't delete all groups in advanced mode because slim mode always ensures there's a group, even if empty
        if (lastFishCatchCfg.IgnoreConditionSet is { } ignoreSet && ignoreSet.HasAnyCondition() && ignoreSet.PassesOrUnconfigured())
            return false;

        if (Ws.Fishing.LastCatch is { } lc && lc.FishId > 0)
            lastFishCatchCfg.SparefulHand.FishIdToCheck = lc.FishId;

        if (lastFishCatchCfg.IdenticalCast.IsAvailableToCast())
            cast = lastFishCatchCfg.IdenticalCast;

        if (lastFishCatchCfg.SurfaceSlap.IsAvailableToCast())
            cast = lastFishCatchCfg.SurfaceSlap;

        if (lastFishCatchCfg.SparefulHand.IsAvailableToCast())
            cast = lastFishCatchCfg.SparefulHand;

        var multiHook = lastFishCatchCfg.Multihook;

        if (cast == null && multiHook.Enabled && multiHook.CastCondition()) {
            Service.TaskManager.Enqueue(() => PlayerRes.CastActionDelayed(multiHook.Id, multiHook.ActionType, multiHook.GetName()));
            Service.TaskManager.Enqueue(() => CastLineMoochOrRelease(GetAutoCastCfg(), lastFishCatchCfg));
            return true;
        }

        if (cast != null) {
            if (multiHook.Enabled && multiHook.CastCondition()) {
                Service.TaskManager.Enqueue(() => PlayerRes.CastActionDelayed(multiHook.Id, multiHook.ActionType, multiHook.GetName()));
                Service.TaskManager.Enqueue(() => PlayerRes.CastActionDelayed(cast.Id, cast.ActionType, cast.GetName()));
                return true;
            }

            PlayerRes.CastActionDelayed(cast.Id, cast.ActionType, cast.GetName());
            return true;
        }

        return false;
    }

    private void CheckFishCaughtSwap(FishConfig? lastCatchCfg) {
        if (lastCatchCfg == null || !lastCatchCfg.Enabled)
            return;

        var guid = lastCatchCfg.UniqueId;

        var (swapPresetEnabled, _) = lastCatchCfg.SwapPresetLimit.Value;

        if (swapPresetEnabled && lastCatchCfg.SwapPresetLimit.BackingSet.HasGroups()
            && Presets.CurrentPreset.PresetName == lastCatchCfg.PresetToSwap)
            FishingHelper.RemovePresetSwap(guid);

        if (swapPresetEnabled && lastCatchCfg.SwapPresetLimit.BackingSet.Passes() && !FishingHelper.SwappedPreset(guid) && !Ws.FishingStep.HasFlag(FishingSteps.PresetSwapped)) {
            if (lastCatchCfg.PresetToSwap == Presets.CurrentPreset.PresetName) {
                FindPresetByName(lastCatchCfg.PresetToSwap)?.TryResetCounter();
            }
            else if (lastCatchCfg.PresetToSwap != Presets.CurrentPreset.PresetName) {
                var preset = FindPresetByName(lastCatchCfg.PresetToSwap);

                FishingHelper.AddPresetSwap(guid);
                Ws.Execute(new WorldState.OpSetFishingStep(FishingSteps.PresetSwapped, Or: true));

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

        var (swapBaitEnabled, _) = lastCatchCfg.SwapBaitLimit.Value;

        if (swapBaitEnabled && lastCatchCfg.SwapBaitLimit.BackingSet.Passes() && !FishingHelper.SwappedBait(guid) && !Ws.FishingStep.HasFlag(FishingSteps.BaitSwapped)) {
            if (lastCatchCfg.BaitToSwap.Id != Ws.Fishing.BaitInfo.BaitId) {
                var result = ChangeBait(lastCatchCfg.BaitToSwap);

                FishingHelper.AddBaitSwap(guid);
                Ws.Execute(new WorldState.OpSetFishingStep(FishingSteps.BaitSwapped, Or: true));
                if (result == ChangeBaitReturn.Success) {
                    Service.PrintChat(@$"[Fish Caught] Swapping bait to {lastCatchCfg.BaitToSwap.Name}");
                    Service.Save();
                }
                if (lastCatchCfg.SwapBaitResetCount) FishingHelper.ToBeRemoved.Add(guid);
            }
        }
    }
}
