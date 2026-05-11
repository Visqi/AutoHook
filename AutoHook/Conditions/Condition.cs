using Newtonsoft.Json;
using System.Threading;

namespace AutoHook.Conditions;

/// <summary>
/// One condition: type id + minimal params. Only serialized keys are stored.
/// </summary>
public class Condition {
    [JsonIgnore]
    private static int _nextUiId = 1;

    /// <summary>Registry key, e.g. "StatusActive", "BiteTimer", "Weather".</summary>
    [JsonProperty("t")]
    public string TypeId { get; set; } = "";

    /// <summary>Type-specific params. Only include non-default keys when serializing.</summary>
    [JsonProperty("p")]
    [JsonConverter(typeof(ConditionParamConverter))]
    public Dictionary<string, object> Params { get; set; } = [];

    /// <summary>When false, this condition is skipped in evaluation (UI can use for toggle-without-delete).</summary>
    [JsonProperty("e")]
    public bool Enabled { get; set; } = true;

    /// <summary>UI-only identifier used for stable ImGui IDs; prevents reusing open/closed state across deleted/recreated conditions.</summary>
    [JsonIgnore]
    public int UiId { get; set; }

    public void EnsureUiId() {
        if (UiId <= 0)
            UiId = Interlocked.Increment(ref _nextUiId);
    }

    public bool Evaluate(WorldState world, ConditionRegistry registry)
        => Enabled && registry.Get(TypeId) is { } def && def.Evaluate(world, Params);
}
