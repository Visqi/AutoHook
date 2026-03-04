using Newtonsoft.Json;

namespace AutoHook.Conditions;

/// <summary>
/// One condition: type id + minimal params. Only serialized keys are stored.
/// </summary>
public class Condition
{
    /// <summary>Registry key, e.g. "StatusActive", "BiteTimer", "Weather".</summary>
    [JsonProperty("t")]
    public string TypeId { get; set; } = "";

    /// <summary>Type-specific params. Only include non-default keys when serializing.</summary>
    [JsonProperty("p")]
    [JsonConverter(typeof(ConditionParamConverter))]
    public Dictionary<string, object> Params { get; set; } = [];

    public bool Evaluate(WorldState world, ConditionRegistry registry)
        => registry.Get(TypeId) is { } def && def.Evaluate(world, Params);
}
