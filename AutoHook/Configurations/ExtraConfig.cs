using System.Threading;
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
    [JsonIgnore]
    private static int _nextUiId = 1;

    public bool Enabled { get; set; } = true;

    public int UiId { get; set; }

    public ConditionSet? ConditionSet { get; set; }

    public bool SwapPreset { get; set; }
    public string PresetToSwap { get; set; } = @"-";

    public bool SwapBait { get; set; }
    public BaitFishClass BaitToSwap { get; set; } = new();

    public ExtraStopAction StopAction { get; set; } = ExtraStopAction.None;

    public void EnsureUiId()
    {
        if (UiId <= 0)
            UiId = Interlocked.Increment(ref _nextUiId);
    }
}

public class ExtraConfig : BaseOption
{
    public bool Enabled = false;

    public bool ResetCounterPresetSwap = false;
    public bool ForceBaitSwap;
    public int ForcedBaitId;

    public List<ExtraTrigger> Triggers { get; set; } = [];

    /// <summary>Per-trigger last evaluation result (not serialized) used to detect BecomesTrue/BecomesFalse.</summary>
    [JsonIgnore] public List<bool> LastTriggerStates { get; } = [];

    [Obsolete("Legacy config")] public bool SwapBaitIntuitionGain = false;
    [Obsolete("Legacy config")] public BaitFishClass BaitToSwapIntuitionGain = new();

    [Obsolete("Legacy config")] public bool SwapBaitIntuitionLost = false;
    [Obsolete("Legacy config")] public BaitFishClass BaitToSwapIntuitionLost = new();

    [Obsolete("Legacy config")] public bool SwapPresetIntuitionGain = false;
    [Obsolete("Legacy config")] public string PresetToSwapIntuitionGain = @"-";

    [Obsolete("Legacy config")] public bool SwapPresetIntuitionLost = false;
    [Obsolete("Legacy config")] public string PresetToSwapIntuitionLost = @"-";

    [Obsolete("Legacy config")] public bool SwapBaitSpectralCurrentGain = false;
    [Obsolete("Legacy config")] public BaitFishClass BaitToSwapSpectralCurrentGain = new();

    [Obsolete("Legacy config")] public bool SwapBaitSpectralCurrentLost = false;
    [Obsolete("Legacy config")] public BaitFishClass BaitToSwapSpectralCurrentLost = new();

    [Obsolete("Legacy config")] public bool SwapPresetSpectralCurrentGain = false;
    [Obsolete("Legacy config")] public string PresetToSwapSpectralCurrentGain = @"-";

    [Obsolete("Legacy config")] public bool SwapPresetSpectralCurrentLost = false;
    [Obsolete("Legacy config")] public string PresetToSwapSpectralCurrentLost = @"-";

    [Obsolete("Legacy config")] public bool QuitOnIntuitionLost = false;
    [Obsolete("Legacy config")] public bool StopOnIntuitionLost = false;

    // Angler's Art
    [Obsolete("Legacy config")] public bool StopAfterAnglersArt = false;
    [Obsolete("Legacy config")] public int AnglerStackQtd = 0;
    [Obsolete("Legacy config")] public FishingSteps AnglerStopFishingStep = FishingSteps.None;
    [Obsolete("Legacy config")] public bool SwapBaitAnglersArt = false;
    [Obsolete("Legacy config")] public BaitFishClass BaitToSwapAnglersArt = new();
    [Obsolete("Legacy config")] public bool SwapPresetAnglersArt = false;
    [Obsolete("Legacy config")] public string PresetToSwapAnglersArt = @"-";

    // Swimbait
    [Obsolete("Legacy config")] public SwimbaitAction SwimbaitFillsAction = SwimbaitAction.None;
    [Obsolete("Legacy config")] public string PresetToSwapSwimbaitFills = @"-";
    [Obsolete("Legacy config")] public SwimbaitAction SwimbaitRunsOutAction = SwimbaitAction.None;
    [Obsolete("Legacy config")] public string PresetToSwapSwimbaitRunsOut = @"-";

    public override void DrawOptions() { }
}
