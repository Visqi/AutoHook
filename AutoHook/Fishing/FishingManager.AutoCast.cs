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
            return false;

        if (Service.BaitManager.CurrentSwimBait is not { })
            return false;

        HookConfig? swimbaitMoochConfig = null;
        if (Presets.SelectedPreset != null)
            swimbaitMoochConfig = Presets.SelectedPreset.GetCfgById((int)Service.BaitManager.CurrentSwimBait, true);

        // If no config found in selected preset, or swimbait not enabled, check global preset config
        if (swimbaitMoochConfig == null || !swimbaitMoochConfig.Enabled || !swimbaitMoochConfig.UseSwimbait)
        {
            if (Presets.DefaultPreset.ListOfMooch.FirstOrDefault(hook => hook.BaitFish.Id == GameRes.AllMoochesId) is { Enabled: true, UseSwimbait: true } global)
                swimbaitMoochConfig = global;
            else
                return false;
        }

        if (Service.BaitManager.GetSwimbaitCountForFish((uint)Service.BaitManager.CurrentSwimBait) < swimbaitMoochConfig.SwimbaitCountThreshold)
            return false;

        if (swimbaitMoochConfig.OnlyUseWhenNoMoochAvailable)
        {
            if (!blockMooch)
            {
                if (lastFishCatchCfg is { Enabled: true } && lastFishCatchCfg.Mooch.IsAvailableToCast())
                    return false;
                if (acCfg.CastMooch.IsAvailableToCast())
                    return false;
            }
        }

        foreach (var (id, i) in Service.BaitManager.SwimbaitIds.WithIndex())
        {
            if (id == Service.BaitManager.CurrentSwimBait)
            {
                if (Service.BaitManager.ChangeSwimbait((uint)i) == BaitManager.ChangeBaitReturn.Success)
                {
                    Service.PrintDebug($"[Swimbait] Using swimbait slot {i} (fish ID: {Service.BaitManager.CurrentSwimBait})");
                    Service.Status = $"Using swimbait: {MultiString.GetItemName((int)Service.BaitManager.CurrentSwimBait)}";
                    return true;
                }
            }
        }

        return false;
    }
}
