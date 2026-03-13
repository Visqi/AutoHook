using AutoHook.Conditions;
using Newtonsoft.Json;
using System.Threading;

namespace AutoHook.Configurations;

public enum ExtraStopAction {
    None,
    StopOnly,
    QuitFishing,
}

public class ExtraTrigger {
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

    public bool ResolveCollectablesWindow { get; set; }
    public bool ResolveCollectablesForceNo { get; set; }

    public bool StartFishing { get; set; }

    public void EnsureUiId() {
        if (UiId <= 0)
            UiId = Interlocked.Increment(ref _nextUiId);
    }
}

public class ExtraConfig : BaseOption {
    public bool Enabled = false;

    public bool ResetCounterPresetSwap = false;
    public bool ForceBaitSwap;
    public int ForcedBaitId;

    public List<ExtraTrigger> Triggers { get; set; } = [];

    [JsonIgnore] public List<bool> LastTriggerStates { get; } = [];

    public override void DrawOptions() { }
}
