using AutoHook.Conditions;
using ECommons.Throttlers;
using Lumina.Excel.Sheets;

namespace AutoHook.Fishing;

public partial class FishingManager {
    public AutoCastsConfig GetAutoCastCfg()
        => Presets.SelectedPreset?.AutoCastsCfg ?? Presets.DefaultPreset.AutoCastsCfg;

    private void CheckWhileFishingActions() {
        if (_fishingTimer.IsRunning) {
            var elapsed = Math.Truncate(_fishingTimer.ElapsedMilliseconds / 1000.0 * 100) / 100;
            var chum = Ws.Player.HasStatus(IDs.Status.Chum);
            if (Math.Abs(Ws.Fishing.BiteInfo.BiteTimeSeconds - elapsed) >= 0.01 || Ws.Fishing.ChumActive != chum)
                Ws.Execute(new FishingInfo.OpBiteContext(elapsed, chum));
        }

        if (!EzThrottler.Throttle("CheckWhileFishingActions", 200) || _spectralRestPending)
            return;

        var hookCfg = GetHookCfg();

        if (!hookCfg.Enabled)
            return;

        hookCfg.GetHookset().CastLures.TryCasting(Ws.Fishing.LureSuccess);
    }

    private bool TryCastCollectBeforeLine(AutoCastsConfig acCfg) {
        if (!acCfg.EnableAll || !acCfg.CastCollect.Enabled || Ws.Player.HasStatus(IDs.Status.CollectorsGlove))
            return false;

        if (!acCfg.CastCollect.IsAvailableToCast())
            return false;

        return acCfg.TryCastAction(acCfg.CastCollect);
    }

    private void CastCollectAfterLine() {
        var cfg = GetAutoCastCfg();

        if (Ws.Player.HasStatus(IDs.Status.CollectorsGlove) && cfg.RecastAnimationCancel && cfg.TurnCollectOff && !cfg.CastCollect.Enabled)
            PlayerRes.CastAction(IDs.Actions.Collect);
        else if (Ws.Player.HasStatus(IDs.Status.CollectorsGlove) && cfg.TurnCollectOffWithoutAnimCancel && !cfg.CastCollect.Enabled)
            PlayerRes.CastAction(IDs.Actions.Collect);
        else
            cfg.TryCastAction(cfg.CastCollect);
    }

    private void UseAutoCasts() {
        if (Ws.Fishing.FishingStep.HasFlag(FishingSteps.None) || Ws.Fishing.FishingStep.HasFlag(FishingSteps.BeganFishing) || Ws.Fishing.FishingStep.HasFlag(FishingSteps.Quitting))
            return;

        if (!Ws.IsCastAvailable() || Service.TaskManager.IsBusy)
            return;

        Service.TaskManager.Enqueue(() => {
            var lastFishCatchCfg = GetEffectiveCatchConfig();
            var acCfg = GetAutoCastCfg();
            var ignoreMooch = lastFishCatchCfg?.NeverMooch ?? false;
            var autoCast = acCfg.GetNextAutoCast(ignoreMooch);

            if (acCfg.TryCastAction(autoCast, false, ignoreMooch)) {
                ContinueStartFishing(autoCast);
                return;
            }

            if (Service.WorldStateUpdater.HasPendingGp)
                return;

            CastLineMoochOrRelease(acCfg, lastFishCatchCfg);
        }, "AutoCasting");
    }

    private void ContinueStartFishing(BaseActionCast? usedAction) {
        if (!Ws.Fishing.FishingStep.HasFlag(FishingSteps.StartedCasting) || Ws.Fishing.FishingStep.HasFlag(FishingSteps.BeganFishing))
            return;

        var delay = usedAction != null ? PlayerRes.GetPostCastDelayMs() : 0;
        Service.TaskManager.EnqueueDelay(delay);
        Service.TaskManager.Enqueue(() => {
            if (!Ws.Fishing.FishingStep.HasFlag(FishingSteps.StartedCasting) || Ws.Fishing.FishingStep.HasFlag(FishingSteps.BeganFishing))
                return;

            UseAutoCasts();
        }, "ContinueStartFishing");
    }

    private void CastLineMoochOrRelease(AutoCastsConfig acCfg, FishConfig? lastFishCatchCfg) {
        if (TryCastCollectBeforeLine(acCfg))
            return;

        var blockMooch = lastFishCatchCfg is { Enabled: true, NeverMooch: true };

        if (TryMoochBeforeSwimbaitForSameFish(acCfg, lastFishCatchCfg, blockMooch))
            return;

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

    // same-fish mooch doesn't eat swimbait; prefer mooch when last catch is also in a swimbait slot.
    private bool TryMoochBeforeSwimbaitForSameFish(AutoCastsConfig acCfg, FishConfig? lastFishCatchCfg, bool blockMooch) {
        if (blockMooch)
            return false;

        if (Ws.Fishing.LastCatch is not { FishId: > 0 } lastCatch)
            return false;

        var fishId = lastCatch.FishId;
        if (!Ws.Fishing.SwimbaitIds.Any(id => id == fishId))
            return false;

        if (lastFishCatchCfg is { Enabled: true } && lastFishCatchCfg.Mooch.IsAvailableToCast()) {
            PlayerRes.CastActionNoDelay(lastFishCatchCfg.Mooch.Id, lastFishCatchCfg.Mooch.ActionType, UIStrings.Mooch);
            return true;
        }

        return acCfg.TryCastAction(acCfg.CastMooch, true);
    }

    private bool TryUseSwimbait(AutoCastsConfig acCfg, FishConfig? lastFishCatchCfg, bool blockMooch) {
        if (Ws.GetSwimbaitCount() is 0)
            return false;

        var presetName = Presets.SelectedPreset?.PresetName ?? "(none)";

        foreach (var (fishId, slotIndex) in Ws.Fishing.SwimbaitIds.ToArray().WithIndex()) {
            if (fishId == 0)
                continue;

            HookConfig? swimbaitMoochConfig = null;
            if (Presets.SelectedPreset != null)
                swimbaitMoochConfig = Presets.SelectedPreset.GetCfgById(fishId, true);

            SwimbaitConfig? activeSwimbaitCfg = null;
            var configSource = "none";

            if (swimbaitMoochConfig != null && swimbaitMoochConfig.Enabled) {
                var useIntuitionTab = swimbaitMoochConfig.UsesIntuitionHookConfig();
                activeSwimbaitCfg = swimbaitMoochConfig.GetSwimbaitConfig();
                configSource = $"preset ({swimbaitMoochConfig.BaitFish.Name}, {(useIntuitionTab ? "intuition" : "normal")} tab)";
            }

            if (activeSwimbaitCfg == null || !activeSwimbaitCfg.UseSwimbait) {
                var globalAllMooches = Presets.DefaultPreset.ListOfMooch.FirstOrDefault(hook => hook.BaitFish.Id == GameRes.AllMoochesId);
                if (globalAllMooches != null && globalAllMooches.Enabled) {
                    var globalCfg = globalAllMooches.GetSwimbaitConfig();
                    if (globalCfg.UseSwimbait) {
                        swimbaitMoochConfig = globalAllMooches;
                        activeSwimbaitCfg = globalCfg;
                        configSource = $"global All Mooches ({(globalAllMooches.UsesIntuitionHookConfig() ? "intuition" : "normal")} tab)";
                    }
                }

                if (activeSwimbaitCfg == null || !activeSwimbaitCfg.UseSwimbait)
                    continue;
            }

            var fishName = fishId == 0 ? "unknown fish" : Item.GetRow(fishId).Name.ToString();
            Ws.SwimbaitEvaluationFishId = fishId;
            try {
                if (activeSwimbaitCfg.ConditionSet.Fails()) {
                    Ws.Decide(DecisionContext.Swimbait, false, fishName,
                        JoinSwimbaitDetail(configSource, activeSwimbaitCfg.ConditionSet?.Describe()), presetName);
                    continue;
                }
            }
            finally {
                Ws.SwimbaitEvaluationFishId = 0;
            }

            if (ChangeSwimbait((uint)slotIndex) == ChangeBaitReturn.Success) {
                Ws.Decide(DecisionContext.Swimbait, true, $"Slot {slotIndex}",
                    JoinSwimbaitDetail($"{fishName} · {configSource}", activeSwimbaitCfg.ConditionSet?.Describe()), presetName);
                Service.WorldStateUpdater?.RefreshFishingStateSnapshot();
                UpdateStatusAndTimer();
                Service.Status = $"Using swimbait: {Item.GetRow(fishId).Name}";
                return true;
            }

            Ws.Decide(DecisionContext.Swimbait, false, $"Slot {slotIndex}",
                JoinSwimbaitDetail($"{fishName} · ChangeSwimbait failed · {configSource}", null), presetName);
        }

        return false;
    }

    private static string? JoinSwimbaitDetail(string? head, string? conditions) {
        if (string.IsNullOrEmpty(head))
            return string.IsNullOrEmpty(conditions) ? null : conditions;
        return string.IsNullOrEmpty(conditions) ? head : $"{head}\n{conditions}";
    }
}
