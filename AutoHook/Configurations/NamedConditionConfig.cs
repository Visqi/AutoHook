using AutoHook.Conditions;
using Newtonsoft.Json;
using System.Threading;

namespace AutoHook.Configurations;

public class NamedConditionConfig {
    [JsonIgnore]
    private static int _nextUiId = 1;

    public Guid UniqueId { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = "";

    [JsonProperty("s")]
    public ConditionSet ConditionSet { get; set; } = new();

    [JsonIgnore]
    public int UiId { get; set; }

    public void EnsureUiId() {
        if (UiId <= 0)
            UiId = Interlocked.Increment(ref _nextUiId);
    }
}
