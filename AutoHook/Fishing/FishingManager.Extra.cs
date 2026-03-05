using AutoHook.Conditions;

namespace AutoHook.Fishing;

public partial class FishingManager
{
    public ExtraConfig GetExtraCfg()
    {
        return Presets.SelectedPreset?.ExtraCfg.Enabled ?? false
            ? Presets.SelectedPreset.ExtraCfg
            : Presets.DefaultPreset.ExtraCfg;
    }

    private void CheckExtraActions(ExtraConfig extraCfg)
    {
        // Only trigger-based behavior runs at runtime. Legacy Extra fields are migration-only.
        if (extraCfg.Triggers.Count > 0)
            RunExtraTriggers(extraCfg);
    }

    private void CheckSpectral(ExtraConfig extraCfg)
    {
        if (Ws.SpectralCurrentStatus == SpectralCurrentStatus.NotActive)
        {
            if (!Ws.OceanFishing.SpectralCurrentActive)
                return;

            Ws.Execute(new WorldState.OpOceanFishing(Ws.OceanFishing.WithSpectralCurrentActive(true)));

            if (!extraCfg.Enabled)
                return;

            if (extraCfg.SwapPresetSpectralCurrentGain && !Ws.FishingStep.HasFlag(FishingSteps.PresetSwapped))
            {
                var preset =
                    Presets.CustomPresets.FirstOrDefault(preset =>
                        preset.PresetName == extraCfg.PresetToSwapSpectralCurrentGain);

                Ws.Execute(new WorldState.OpOrFishingStep(FishingSteps.PresetSwapped));
                if (preset != null)
                {
                    Service.Save();
                    Presets.SelectedPreset = preset;
                    Service.PrintChat(
                        @$"[Extra] Spectral Current Active: Swapping preset to {extraCfg.PresetToSwapSpectralCurrentGain}");
                    Service.Save();
                }
                else
                    Service.PrintChat(@$"Preset {extraCfg.PresetToSwapSpectralCurrentGain} not found.");
            }

            if (extraCfg.SwapBaitSpectralCurrentGain && !Ws.FishingStep.HasFlag(FishingSteps.BaitSwapped))
            {
                var result = Service.BaitManager.ChangeBait(extraCfg.BaitToSwapSpectralCurrentGain);

                Ws.Execute(new WorldState.OpOrFishingStep(FishingSteps.BaitSwapped));
                if (result == BaitManager.ChangeBaitReturn.Success)
                {
                    Service.PrintChat(
                        @$"[Extra] Spectral Current Active: Swapping bait to {extraCfg.BaitToSwapSpectralCurrentGain.Name}");
                    Service.Save();
                }
            }
        }

        if (Ws.SpectralCurrentStatus == SpectralCurrentStatus.Active)
        {
            if (Ws.OceanFishing.SpectralCurrentActive)
                return;

            Ws.Execute(new WorldState.OpOceanFishing(Ws.OceanFishing.WithSpectralCurrentActive(false)));

            if (!extraCfg.Enabled)
                return;

            if (extraCfg.SwapPresetSpectralCurrentLost && !Ws.FishingStep.HasFlag(FishingSteps.PresetSwapped))
            {
                var preset =
                    Presets.CustomPresets.FirstOrDefault(preset =>
                        preset.PresetName == extraCfg.PresetToSwapSpectralCurrentLost);

                Ws.Execute(new WorldState.OpOrFishingStep(FishingSteps.PresetSwapped));

                if (preset != null)
                {
                    Service.Save();
                    Presets.SelectedPreset = preset;
                    Service.PrintChat(
                        @$"[Extra] Spectral Current Ended: Swapping preset to {extraCfg.PresetToSwapIntuitionLost}");
                    Service.Save();
                }
                else
                    Service.PrintChat(@$"Preset {extraCfg.SwapPresetSpectralCurrentLost} not found.");
            }

            if (extraCfg.SwapBaitSpectralCurrentLost && !Ws.FishingStep.HasFlag(FishingSteps.BaitSwapped))
            {
                var result = Service.BaitManager.ChangeBait(extraCfg.BaitToSwapSpectralCurrentLost);

                Ws.Execute(new WorldState.OpOrFishingStep(FishingSteps.BaitSwapped));

                if (result == BaitManager.ChangeBaitReturn.Success)
                {
                    Service.PrintChat(
                        @$"[Extra] Spectral Current Ended: Swapping bait to {extraCfg.BaitToSwapSpectralCurrentLost.Name}");
                    Service.Save();
                }
            }
        }
    }

    private void CheckIntuition(ExtraConfig extraCfg)
    {
        if (Ws.IntuitionStatus == IntuitionStatus.NotActive)
        {
            if (!Ws.HasStatus(IDs.Status.FishersIntuition))
                return;

            Ws.Execute(new WorldState.OpIntuition(IntuitionStatus.Active, Ws.GetStatusTime(IDs.Status.FishersIntuition)));

            if (!extraCfg.Enabled)
                return;
            ExtraCfgGainedIntuition(extraCfg);
        }

        if (Ws.IntuitionStatus == IntuitionStatus.Active)
        {
            if (Ws.HasStatus(IDs.Status.FishersIntuition))
                return;

            Ws.Execute(new WorldState.OpIntuition(IntuitionStatus.NotActive, 0));

            if (!extraCfg.Enabled)
                return;

            ExtraCfgLostIntuition(extraCfg);
        }
    }

    private void ExtraCfgGainedIntuition(ExtraConfig extraCfg)
    {
        if (extraCfg.SwapPresetIntuitionGain && !Ws.FishingStep.HasFlag(FishingSteps.PresetSwapped))
        {
            var preset = Presets.CustomPresets.FirstOrDefault(preset =>
                preset.PresetName == extraCfg.PresetToSwapIntuitionGain);

            Ws.Execute(new WorldState.OpOrFishingStep(FishingSteps.PresetSwapped));
            if (preset != null)
            {
                Service.Save();
                Presets.SelectedPreset = preset;
                Service.PrintChat(
                    @$"[Extra] Intuition Active - Swapping preset to {extraCfg.PresetToSwapIntuitionGain}");
                Service.Save();
            }
            else
                Service.PrintChat(
                    @$"[Extra] Intuition Active - Preset {extraCfg.PresetToSwapIntuitionGain} not found.");
        }

        if (extraCfg.SwapBaitIntuitionGain && !Ws.FishingStep.HasFlag(FishingSteps.BaitSwapped))
        {
            var result = Service.BaitManager.ChangeBait(extraCfg.BaitToSwapIntuitionGain);

            Ws.Execute(new WorldState.OpOrFishingStep(FishingSteps.BaitSwapped));

            if (result == BaitManager.ChangeBaitReturn.Success)
            {
                Service.PrintChat(
                    @$"[Extra] Intuition Active - Swapping bait to {extraCfg.BaitToSwapIntuitionGain.Name}");
                Service.Save();
            }
        }
    }

    private void ExtraCfgLostIntuition(ExtraConfig extraCfg)
    {
        if (extraCfg.SwapPresetIntuitionLost && !Ws.FishingStep.HasFlag(FishingSteps.PresetSwapped))
        {
            var preset =
                Presets.CustomPresets.FirstOrDefault(preset =>
                    preset.PresetName == extraCfg.PresetToSwapIntuitionLost);

            Ws.Execute(new WorldState.OpOrFishingStep(FishingSteps.PresetSwapped));

            if (preset != null)
            {
                Service.Save();
                // one try per catch
                Presets.SelectedPreset = preset;
                Service.PrintChat(@$"[Extra] Intuition Lost - Swapping preset to {extraCfg.PresetToSwapIntuitionLost}");
                Service.Save();
            }
            else
                Service.PrintChat(@$"[Extra] Intuition Lost - Preset {extraCfg.PresetToSwapIntuitionLost} not found.");
        }

        if (extraCfg.SwapBaitIntuitionLost && !Ws.FishingStep.HasFlag(FishingSteps.BaitSwapped))
        {
            var result = Service.BaitManager.ChangeBait(extraCfg.BaitToSwapIntuitionLost);

            Ws.Execute(new WorldState.OpOrFishingStep(FishingSteps.BaitSwapped));
            if (result == BaitManager.ChangeBaitReturn.Success)
            {
                Service.PrintChat(
                    @$"[Extra] Intuition Lost - Swapping bait to {extraCfg.BaitToSwapIntuitionLost.Name}");
                Service.Save();
            }
        }

        if (extraCfg.QuitOnIntuitionLost)
            Ws.Execute(new WorldState.OpSetFishingStep(FishingSteps.Quitting));

        if (extraCfg.StopOnIntuitionLost)
            Ws.Execute(new WorldState.OpSetFishingStep(FishingSteps.None));
    }

    private void RunExtraTriggers(ExtraConfig extraCfg)
    {
        for (var i = 0; i < extraCfg.Triggers.Count; i++)
        {
            var trig = extraCfg.Triggers[i];
            if (trig.ConditionSet == null)
                continue;

            var current = trig.ConditionSet.Evaluate(Ws, ConditionRegistry.Registry);
            var last = i < extraCfg.LastTriggerStates.Count ? extraCfg.LastTriggerStates[i] : current;

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

    private void ExecuteExtraTriggerActions(ExtraConfig extraCfg, ExtraTrigger trig)
    {
        // Stop/quit fishing
        if (trig.StopAction == ExtraStopAction.StopOnly)
        {
            Ws.Execute(new WorldState.OpSetFishingStep(FishingSteps.None));
        }
        else if (trig.StopAction == ExtraStopAction.QuitFishing)
        {
            Ws.Execute(new WorldState.OpSetFishingStep(FishingSteps.Quitting));
        }

        // Swap preset
        if (trig.SwapPreset && !Ws.FishingStep.HasFlag(FishingSteps.PresetSwapped))
        {
            var preset = Presets.CustomPresets
                .FirstOrDefault(p => p.PresetName == trig.PresetToSwap);

            Ws.Execute(new WorldState.OpOrFishingStep(FishingSteps.PresetSwapped));

            if (preset != null)
            {
                Service.Save();
                Presets.SelectedPreset = preset;
                Service.PrintChat(@$"[Extra] Trigger: Swapping preset to {trig.PresetToSwap}");
                Service.Save();
            }
            else if (!string.IsNullOrEmpty(trig.PresetToSwap) && trig.PresetToSwap != @"-")
            {
                Service.PrintChat(@$"[Extra] Trigger: Preset {trig.PresetToSwap} not found.");
            }
        }

        // Swap bait
        if (trig.SwapBait && !Ws.FishingStep.HasFlag(FishingSteps.BaitSwapped))
        {
            var result = Service.BaitManager.ChangeBait(trig.BaitToSwap);
            Ws.Execute(new WorldState.OpOrFishingStep(FishingSteps.BaitSwapped));

            if (result == BaitManager.ChangeBaitReturn.Success)
            {
                Service.PrintChat(@$"[Extra] Trigger: Swapping bait to {trig.BaitToSwap.Name}");
                Service.Save();
            }
        }
    }

    private void CheckAnglersArt(ExtraConfig extraCfg)
    {
        if (!Ws.HasAnglersArtStacks(extraCfg.AnglerStackQtd))
            return;

        if (extraCfg.SwapPresetAnglersArt && !Ws.FishingStep.HasFlag(FishingSteps.PresetSwapped))
        {
            var preset =
                Presets.CustomPresets.FirstOrDefault(preset =>
                    preset.PresetName == extraCfg.PresetToSwapAnglersArt);

            Ws.Execute(new WorldState.OpOrFishingStep(FishingSteps.PresetSwapped));

            if (preset != null)
            {
                Service.Save();
                Presets.SelectedPreset = preset;
                Service.PrintChat(
                    @$"[Extra] Angler's Stack - Swapping preset to {extraCfg.PresetToSwapAnglersArt}");
                Service.Save();
            }
            else
                Service.PrintChat(@$"[Extra] Anglers Stack - Preset {extraCfg.PresetToSwapAnglersArt} not found.");
        }

        if (extraCfg.SwapBaitAnglersArt && !Ws.FishingStep.HasFlag(FishingSteps.BaitSwapped))
        {
            var result = Service.BaitManager.ChangeBait(extraCfg.BaitToSwapAnglersArt);
            Ws.Execute(new WorldState.OpOrFishingStep(FishingSteps.BaitSwapped));
            if (result == BaitManager.ChangeBaitReturn.Success)
            {
                Service.PrintChat(
                    @$"[Extra] Angler's Stack - Swapping bait to {extraCfg.BaitToSwapAnglersArt.Name}");
                Service.Save();
            }
        }
    }

    private int _lastSwimbaitCount = -1;

    private void CheckSwimbait(ExtraConfig extraCfg)
    {
        if (!extraCfg.Enabled)
            return;

        var currentSwimbaitCount = Ws.GetSwimbaitCount();

        // Only check on state change
        if (_lastSwimbaitCount == currentSwimbaitCount)
            return;

        // Check if swimbait filled (0 -> 3 or any increase to 3)
        if (currentSwimbaitCount >= 3 && _lastSwimbaitCount < 3 && extraCfg.SwimbaitFillsAction != SwimbaitAction.None)
        {
            if (extraCfg.SwimbaitFillsAction == SwimbaitAction.SwapPreset && !Ws.FishingStep.HasFlag(FishingSteps.PresetSwapped))
            {
                var preset = Presets.CustomPresets.FirstOrDefault(preset =>
                    preset.PresetName == extraCfg.PresetToSwapSwimbaitFills);

                Ws.Execute(new WorldState.OpOrFishingStep(FishingSteps.PresetSwapped));

                if (preset != null)
                {
                    Service.Save();
                    Presets.SelectedPreset = preset;
                    Service.PrintChat(@$"[Extra] Swimbait Filled: Swapping preset to {extraCfg.PresetToSwapSwimbaitFills}");
                    Service.Save();
                }
                else
                    Service.PrintChat(@$"[Extra] Swimbait Filled: Preset {extraCfg.PresetToSwapSwimbaitFills} not found.");
            }
            else if (extraCfg.SwimbaitFillsAction == SwimbaitAction.Stop)
            {
                Ws.Execute(new WorldState.OpSetFishingStep(FishingSteps.None));
                Service.PrintChat(@$"[Extra] Swimbait Filled: Stopping fishing");
            }
        }

        // Check if swimbait ran out (any count -> 0)
        if (currentSwimbaitCount == 0 && _lastSwimbaitCount > 0 && extraCfg.SwimbaitRunsOutAction != SwimbaitAction.None)
        {
            if (extraCfg.SwimbaitRunsOutAction == SwimbaitAction.SwapPreset && !Ws.FishingStep.HasFlag(FishingSteps.PresetSwapped))
            {
                var preset = Presets.CustomPresets.FirstOrDefault(preset =>
                    preset.PresetName == extraCfg.PresetToSwapSwimbaitRunsOut);

                Ws.Execute(new WorldState.OpOrFishingStep(FishingSteps.PresetSwapped));

                if (preset != null)
                {
                    Service.Save();
                    Presets.SelectedPreset = preset;
                    Service.PrintChat(@$"[Extra] Swimbait Ran Out: Swapping preset to {extraCfg.PresetToSwapSwimbaitRunsOut}");
                    Service.Save();
                }
                else
                    Service.PrintChat(@$"[Extra] Swimbait Ran Out: Preset {extraCfg.PresetToSwapSwimbaitRunsOut} not found.");
            }
            else if (extraCfg.SwimbaitRunsOutAction == SwimbaitAction.Stop)
            {
                Ws.Execute(new WorldState.OpSetFishingStep(FishingSteps.None));
                Service.PrintChat(@$"[Extra] Swimbait Ran Out: Stopping fishing");
            }
        }

        _lastSwimbaitCount = currentSwimbaitCount;
    }
}
