using AutoHook.Conditions;
using ECommons.Throttlers;

namespace AutoHook.Fishing;

public partial class FishingManager {
    public AutoCastsConfig GetAutoCastCfg()
        => Presets.SelectedPreset?.AutoCastsCfg ?? Presets.DefaultPreset.AutoCastsCfg;

    private void CheckWhileFishingActions() {
        if (!EzThrottler.Throttle("CheckWhileFishingActions", 500))
            return;

        if (Service.TaskManager.IsBusy)
            return;

        var hookCfg = GetHookCfg();

        if (!hookCfg.Enabled)
            return;

        Service.TaskManager.Enqueue(() => hookCfg.GetHookset().CastLures.TryCasting(Ws.LureSuccess));
    }

    private void CastCollect() {
        var cfg = GetAutoCastCfg();

        if (Ws.HasStatus(IDs.Status.CollectorsGlove) && cfg.RecastAnimationCancel && cfg.TurnCollectOff && !cfg.CastCollect.Enabled)
            PlayerRes.CastAction(IDs.Actions.Collect);
        else if (Ws.HasStatus(IDs.Status.CollectorsGlove) && cfg.TurnCollectOffWithoutAnimCancel && !cfg.CastCollect.Enabled)
            PlayerRes.CastAction(IDs.Actions.Collect);
        else {
            cfg.TryCastAction(cfg.CastCollect);
            return;
        }
    }

    private void UseAutoCasts() {
        if (Ws.FishingStep.HasFlag(FishingSteps.None) || Ws.FishingStep.HasFlag(FishingSteps.BeganFishing) || Ws.FishingStep.HasFlag(FishingSteps.Quitting))
            return;

        if (!Ws.IsCastAvailable() || Service.TaskManager.IsBusy)
            return;

        Service.TaskManager.Enqueue(() => {
            var lastFishCatchCfg = GetLastCatchConfig();

            var acCfg = GetAutoCastCfg();

            var ignoreMooch = lastFishCatchCfg?.NeverMooch ?? false;
            var autoCast = acCfg.GetNextAutoCast(ignoreMooch);

            if (acCfg.TryCastAction(autoCast, false, ignoreMooch))
                return;

            CastLineMoochOrRelease(acCfg, lastFishCatchCfg);
        }, "AutoCasting");
    }

    private void CastLineMoochOrRelease(AutoCastsConfig acCfg, FishConfig? lastFishCatchCfg) {
        var blockMooch = lastFishCatchCfg is { Enabled: true, NeverMooch: true };

        if (TryUseSwimbait(acCfg, lastFishCatchCfg, blockMooch))
            if (acCfg.TryCastAction(acCfg.CastLine, true))
                return;

        if (!blockMooch) {
            if (lastFishCatchCfg is { Enabled: true } && lastFishCatchCfg.Mooch.IsAvailableToCast()) {
                PlayerRes.CastActionNoDelay(lastFishCatchCfg.Mooch.Id, lastFishCatchCfg.Mooch.ActionType,
                    UIStrings.Mooch);
                return;
            }

            if (acCfg.TryCastAction(acCfg.CastMooch, true))
                return;
        }

        if (acCfg.TryCastAction(acCfg.CastLine, true))
            return;
    }

    private bool TryUseSwimbait(AutoCastsConfig acCfg, FishConfig? lastFishCatchCfg, bool blockMooch) {
        if (Ws.GetSwimbaitCount() is 0)
            return false;

        var swimbaitIds = Ws.SwimbaitIds;
        var isIntuition = Ws.IntuitionStatus == IntuitionStatus.Active;
        foreach (var (fishId, slotIndex) in swimbaitIds.ToArray().WithIndex()) {
            if (fishId == 0)
                continue;

            HookConfig? swimbaitMoochConfig = null;
            if (Presets.SelectedPreset != null) {
                swimbaitMoochConfig = Presets.SelectedPreset.GetCfgById((int)fishId, true);
                Service.PrintDebug($"[Swimbait] Found config in selected preset: {swimbaitMoochConfig != null}, Enabled: {swimbaitMoochConfig?.Enabled}");
            }

            // Resolve per-window swimbait config (normal vs Intuition).
            SwimbaitConfig? activeSwimbaitCfg = null;

            if (swimbaitMoochConfig != null && swimbaitMoochConfig.Enabled) {
                activeSwimbaitCfg = isIntuition ? swimbaitMoochConfig.SwimbaitIntuition : swimbaitMoochConfig.SwimbaitNormal;
            }

            // If no config found in selected preset, or swimbait not enabled for this window, check global preset config.
            if (activeSwimbaitCfg == null || !activeSwimbaitCfg.UseSwimbait) {
                var globalAllMooches = Presets.DefaultPreset.ListOfMooch.FirstOrDefault(hook => hook.BaitFish.Id == GameRes.AllMoochesId);
                if (globalAllMooches != null && globalAllMooches.Enabled) {
                    var globalCfg = isIntuition ? globalAllMooches.SwimbaitIntuition : globalAllMooches.SwimbaitNormal;
                    if (globalCfg.UseSwimbait) {
                        swimbaitMoochConfig = globalAllMooches;
                        activeSwimbaitCfg = globalCfg;
                        Service.PrintDebug("[Swimbait] Using global 'All Mooches' config");
                    }
                }

                if (activeSwimbaitCfg == null || !activeSwimbaitCfg.UseSwimbait) {
                    Service.PrintDebug($"[Swimbait] No valid config found for fish {fishId} in this window, trying next slot");
                    continue;
                }
            }

            var swimbaitCountForFish = Ws.GetSwimbaitCountForFish(fishId);
            if (swimbaitCountForFish < activeSwimbaitCfg.CountThreshold)
                continue;

            if (activeSwimbaitCfg.ConditionSet is { Groups.Count: > 0 } condSet &&
                !condSet.Evaluate(Ws, ConditionRegistry.Registry)) {
                continue;
            }

            if (Service.BaitManager.ChangeSwimbait((uint)slotIndex) == BaitManager.ChangeBaitReturn.Success) {
                Service.PrintDebug($"[Swimbait] Using swimbait slot {slotIndex} (fish ID: {fishId})");
                Service.Status = $"Using swimbait: {MultiString.GetItemName((int)fishId)}";
                return true;
            }
        }

        return false;
    }
}
