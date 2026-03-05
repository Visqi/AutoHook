using Newtonsoft.Json;

namespace AutoHook.Conditions;

/// <summary>
/// A group of conditions combined with AND or OR. (X or Y) in "(X or Y) AND (A or B)".
/// </summary>
public class ConditionGroup
{
    [JsonProperty("m")]
    public ConditionCombineMode CombineMode { get; set; } = ConditionCombineMode.All;

    [JsonProperty("c")]
    public List<Condition> Conditions { get; set; } = [];

    public bool Evaluate(WorldState world, ConditionRegistry registry)
    {
        if (Conditions.Count == 0) return true;

        if (CombineMode == ConditionCombineMode.All)
            return Conditions.All(c => c.Evaluate(world, registry));
        else
            return Conditions.Any(c => c.Evaluate(world, registry));
    }
}
