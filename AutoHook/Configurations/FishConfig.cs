using AutoHook.Conditions;
using System.ComponentModel;

namespace AutoHook.Configurations;

public class FishConfig : BaseOption
{
    [DefaultValue(true)]
    public bool Enabled = true;

    [Obsolete("Legacy config")] public bool IgnoreOnIntuition = false;

    public ConditionSet? IgnoreConditionSet { get; set; }

    /// <summary>Stop after caught condition set</summary>
    public ConditionSet? StopConditionSet { get; set; }

    /// <summary>Swap bait after n caught condition set</summary>
    public ConditionSet? SwapBaitConditionSet { get; set; }

    /// <summary>Swap preset after n caught condition set</summary>
    public ConditionSet? SwapPresetConditionSet { get; set; }

    public BaitFishClass Fish = new();

    public bool StopAfterCaught = false;
    public int StopAfterCaughtLimit = 1;
    public bool StopAfterResetCount = false;

    public AutoIdenticalCast IdenticalCast = new();
    public AutoSurfaceSlap SurfaceSlap = new();
    public AutoMooch Mooch = new();
    public AutoSparefulHand SparefulHand = new();
    public AutoMultiHook Multihook = new();

    public bool SwapBait = false;
    public BaitFishClass BaitToSwap = new();
    public int SwapBaitCount = 1;
    public bool SwapBaitResetCount = false;

    public bool SwapPresets = false;
    public string PresetToSwap = "-";
    public int SwapPresetCount = 1;

    public bool NeverMooch = false;

    public FishingSteps StopFishingStep = FishingSteps.None;

    public FishConfig() { }

    public FishConfig(BaitFishClass fish)
    {
        Fish = fish;
        // TODO: ok this is not the best way, but im tired, and it works for now so be nice to me
        Mooch.Name = UIStrings.Always_Mooch;
    }

    public FishConfig(int fishId)
    {
        Fish = new BaitFishClass(fishId);
    }

    public override void DrawOptions() { }
}
