using ECommons.Throttlers;
using AutoHook.Utils;

namespace AutoHook.Fishing;

public partial class FishingManager
{
    public AutoCastsConfig GetAutoCastCfg()
    {
        return Presets.SelectedPreset?.AutoCastsCfg.EnableAll ?? false
            ? Presets.SelectedPreset.AutoCastsCfg
            : Presets.DefaultPreset.AutoCastsCfg;
    }

    private void CheckWhileFishingActions()
    {
        if (!EzThrottler.Throttle("CheckWhileFishingActions", 500))
            return;

        if (Service.TaskManager.IsBusy)
            return;

        var hookCfg = GetHookCfg();

        if (!hookCfg.Enabled)
            return;

        Service.TaskManager.Enqueue(() => hookCfg.GetHookset().CastLures.TryCasting(_lureSuccess));
    }

    private void CastCollect()
    {
        var cfg = GetAutoCastCfg();

        if (PlayerRes.HasStatus(IDs.Status.CollectorsGlove) && cfg.RecastAnimationCancel && cfg.TurnCollectOff &&
            !cfg.CastCollect.Enabled)
        {
            PlayerRes.CastAction(IDs.Actions.Collect);
        }
        else
        {
            cfg.TryCastAction(cfg.CastCollect);
            return;
        }
    }

    private void UseAutoCasts()
    {
        // if _lastStep is FishBit but currentState is FishingState.PoleReady, it means that the fish was hooked, but it escaped.
        if (_lastStep.HasFlag(FishingSteps.None) || _lastStep.HasFlag(FishingSteps.BeganFishing)
                                                 || _lastStep.HasFlag(FishingSteps.Quitting))
        {
            return;
        }

        if (!PlayerRes.IsCastAvailable() || Service.TaskManager.IsBusy)
            return;

        Service.TaskManager.Enqueue(() =>
        {
            var lastFishCatchCfg = GetLastCatchConfig();

            var acCfg = GetAutoCastCfg();

            var ignoreMooch = lastFishCatchCfg?.NeverMooch ?? false;
            var autoCast = acCfg.GetNextAutoCast(ignoreMooch);

            if (acCfg.TryCastAction(autoCast, false, ignoreMooch))
                return;

            CastLineMoochOrRelease(acCfg, lastFishCatchCfg);
        }, "AutoCasting");
    }

    private void CastLineMoochOrRelease(AutoCastsConfig acCfg, FishConfig? lastFishCatchCfg)
    {
        var blockMooch = lastFishCatchCfg is { Enabled: true, NeverMooch: true };

        if (TryUseSwimbait(acCfg, lastFishCatchCfg, blockMooch))
        {
            if (acCfg.TryCastAction(acCfg.CastLine, true))
                return;
        }

        if (!blockMooch)
        {
            if (lastFishCatchCfg is { Enabled: true } && lastFishCatchCfg.Mooch.IsAvailableToCast())
            {
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

    private bool TryUseSwimbait(AutoCastsConfig acCfg, FishConfig? lastFishCatchCfg, bool blockMooch)
    {
        if (Service.BaitManager.GetSwimbaitCount() is 0)
        {
            Service.PrintDebug("[Swimbait] No swimbait available");
            return false;
        }

        var swimbaitIds = Service.BaitManager.SwimbaitIds;
        Service.PrintDebug($"[Swimbait] Checking swimbait slots: [{string.Join(", ", swimbaitIds)}]");

        foreach (var (fishId, slotIndex) in swimbaitIds.WithIndex())
        {
            if (fishId == 0)
                continue;

            Service.PrintDebug($"[Swimbait] Checking slot {slotIndex} with fish ID: {fishId}");

            HookConfig? swimbaitMoochConfig = null;
            if (Presets.SelectedPreset != null)
            {
                swimbaitMoochConfig = Presets.SelectedPreset.GetCfgById((int)fishId, true);
                Service.PrintDebug($"[Swimbait] Found config in selected preset: {swimbaitMoochConfig != null}, Enabled: {swimbaitMoochConfig?.Enabled}, UseSwimbait: {swimbaitMoochConfig?.UseSwimbait}");
            }

            // If no config found in selected preset, or swimbait not enabled, check global preset config
            if (swimbaitMoochConfig == null || !swimbaitMoochConfig.Enabled || !swimbaitMoochConfig.UseSwimbait)
            {
                var globalAllMooches = Presets.DefaultPreset.ListOfMooch.FirstOrDefault(hook => hook.BaitFish.Id == GameRes.AllMoochesId);
                if (globalAllMooches != null && globalAllMooches.Enabled && globalAllMooches.UseSwimbait)
                {
                    swimbaitMoochConfig = globalAllMooches;
                    Service.PrintDebug("[Swimbait] Using global 'All Mooches' config");
                }
                else
                {
                    Service.PrintDebug($"[Swimbait] No valid config found for fish {fishId}, trying next slot");
                    continue;
                }
            }

            var swimbaitCountForFish = Service.BaitManager.GetSwimbaitCountForFish(fishId);
            Service.PrintDebug($"[Swimbait] Count for fish {fishId}: {swimbaitCountForFish}, Threshold: {swimbaitMoochConfig.SwimbaitCountThreshold}");
            if (swimbaitCountForFish < swimbaitMoochConfig.SwimbaitCountThreshold)
            {
                Service.PrintDebug($"[Swimbait] Count threshold not met, trying next slot");
                continue;
            }

            if (swimbaitMoochConfig.OnlyUseWhenNoMoochAvailable)
            {
                Service.PrintDebug("[Swimbait] Checking 'only use when no mooch available' condition");
                if (!blockMooch)
                {
                    var canMooch = lastFishCatchCfg is { Enabled: true } && lastFishCatchCfg.Mooch.IsAvailableToCast();
                    if (canMooch)
                    {
                        Service.PrintDebug("[Swimbait] Mooch available for current fish, skipping swimbait");
                        continue;
                    }

                    if (acCfg.CastMooch.IsAvailableToCast())
                    {
                        Service.PrintDebug("[Swimbait] Auto mooch available, skipping swimbait");
                        continue;
                    }
                }
            }

            if (Service.BaitManager.ChangeSwimbait((uint)slotIndex) == BaitManager.ChangeBaitReturn.Success)
            {
                Service.PrintDebug($"[Swimbait] Using swimbait slot {slotIndex} (fish ID: {fishId})");
                Service.Status = $"Using swimbait: {MultiString.GetItemName((int)fishId)}";
                return true;
            }
        }

        return false;
    }
}
