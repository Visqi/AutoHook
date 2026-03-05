using AutoHook.Conditions;
using Newtonsoft.Json;

namespace AutoHook.Configurations;

public enum ExtraStopAction
{
    None,
    StopOnly,
    QuitFishing,
}

public class ExtraTrigger
{
    public ConditionSet? ConditionSet { get; set; }

    public bool SwapPreset { get; set; }
    public string PresetToSwap { get; set; } = @"-";

    public bool SwapBait { get; set; }
    public BaitFishClass BaitToSwap { get; set; } = new();

    public ExtraStopAction StopAction { get; set; } = ExtraStopAction.None;
}

public class ExtraConfig : BaseOption
{
    public bool Enabled = false;

    public bool SwapBaitIntuitionGain = false; // legacy
    public BaitFishClass BaitToSwapIntuitionGain = new(); // legacy

    public bool SwapBaitIntuitionLost = false; // legacy
    public BaitFishClass BaitToSwapIntuitionLost = new(); // legacy

    public bool SwapPresetIntuitionGain = false; // legacy
    public string PresetToSwapIntuitionGain = @"-"; // legacy

    public bool SwapPresetIntuitionLost = false; // legacy
    public string PresetToSwapIntuitionLost = @"-"; // legacy

    public bool SwapBaitSpectralCurrentGain = false; // legacy
    public BaitFishClass BaitToSwapSpectralCurrentGain = new(); // legacy

    public bool SwapBaitSpectralCurrentLost = false; // legacy
    public BaitFishClass BaitToSwapSpectralCurrentLost = new(); // legacy

    public bool SwapPresetSpectralCurrentGain = false; // legacy
    public string PresetToSwapSpectralCurrentGain = @"-"; // legacy

    public bool SwapPresetSpectralCurrentLost = false; // legacy
    public string PresetToSwapSpectralCurrentLost = @"-"; // legacy

    public bool ResetCounterPresetSwap = false;
    public bool QuitOnIntuitionLost = false; // legacy
    public bool StopOnIntuitionLost = false; // legacy

    public bool ForceBaitSwap;
    public int ForcedBaitId;

    // Angler's Art
    public bool StopAfterAnglersArt = false; // legacy
    public int AnglerStackQtd = 0; // legacy
    public FishingSteps AnglerStopFishingStep = FishingSteps.None; // legacy
    public bool SwapBaitAnglersArt = false; // legacy
    public BaitFishClass BaitToSwapAnglersArt = new(); // legacy
    public bool SwapPresetAnglersArt = false; // legacy
    public string PresetToSwapAnglersArt = @"-"; // legacy

    // Swimbait
    public SwimbaitAction SwimbaitFillsAction = SwimbaitAction.None; // legacy
    public string PresetToSwapSwimbaitFills = @"-"; // legacy
    public SwimbaitAction SwimbaitRunsOutAction = SwimbaitAction.None; // legacy
    public string PresetToSwapSwimbaitRunsOut = @"-"; // legacy

    /// <summary>
    /// New generic trigger-based extra actions (ConditionSet + Trigger + actions).
    /// When non-empty, legacy fields above are treated as migration-only.
    /// </summary>
    public List<ExtraTrigger> Triggers { get; set; } = [];

    /// <summary>Per-trigger last evaluation result (not serialized) used to detect BecomesTrue/BecomesFalse.</summary>
    [JsonIgnore]
    public List<bool> LastTriggerStates { get; } = [];

    public override void DrawOptions()
    {

    }
}
