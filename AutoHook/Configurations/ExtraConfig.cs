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

    /// <summary>Per-trigger last evaluation result (not serialized) used to detect BecomesTrue/BecomesFalse.</summary>
    [JsonIgnore] public List<bool> LastTriggerStates { get; } = [];

    public override void DrawOptions() { }
}
