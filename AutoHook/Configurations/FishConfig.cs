using AutoHook.Conditions;
using AutoHook.Conditions.Definitions;
using Newtonsoft.Json;

namespace AutoHook.Configurations;

public class FishConfig : BaseOption {
    public bool Enabled = true;

    public ConditionSet? IgnoreConditionSet { get; set; }

    public BaitFishClass Fish = new();

    public bool StopAfterCaught = false;
    public bool StopAfterResetCount = false;

    public AutoIdenticalCast IdenticalCast = new();
    public AutoSurfaceSlap SurfaceSlap = new();
    public AutoMooch Mooch = new();
    public AutoSparefulHand SparefulHand = new();
    public AutoMultiHook Multihook = new();

    public bool SwapBait = false;
    public BaitFishClass BaitToSwap = new();
    public bool SwapBaitResetCount = false;

    public bool SwapPresets = false;
    public string PresetToSwap = "-";

    public bool NeverMooch = false;

    public FishingSteps StopFishingStep = FishingSteps.None;

    public FishConfig() { }

    public FishConfig(BaitFishClass fish) {
        Fish = fish;
        // TODO: ok this is not the best way, but im tired, and it works for now so be nice to me
        Mooch.Name = UIStrings.Always_Mooch;
    }

    public FishConfig(int fishId) {
        Fish = new BaitFishClass(fishId);
    }

    [JsonProperty("StopConditionSet")]
    [JsonConverter(typeof(SingleConditionConverter))]
    public SingleCondition<FishCaughtCountCD, (bool Enabled, int Limit)> StopAfterCaughtLimit { get => field ??= new SingleCondition<FishCaughtCountCD, (bool Enabled, int Limit)>(() => Fish.Id); set; }

    [JsonProperty("SwapBaitConditionSet")]
    [JsonConverter(typeof(SingleConditionConverter))]
    public SingleCondition<FishCaughtCountCD, (bool Enabled, int Limit)> SwapBaitLimit { get => field ??= new SingleCondition<FishCaughtCountCD, (bool Enabled, int Limit)>(() => Fish.Id); set; }

    [JsonProperty("SwapPresetConditionSet")]
    [JsonConverter(typeof(SingleConditionConverter))]
    public SingleCondition<FishCaughtCountCD, (bool Enabled, int Limit)> SwapPresetLimit { get => field ??= new SingleCondition<FishCaughtCountCD, (bool Enabled, int Limit)>(() => Fish.Id); set; }

    public override void DrawOptions() { }
}
