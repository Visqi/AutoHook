using Newtonsoft.Json;

namespace AutoHook.Conditions;

/// <summary>
/// A group of conditions combined with AND or OR. (X or Y) in "(X or Y) AND (A or B)".
/// </summary>
public class ConditionGroup
{
    [JsonProperty("m")]
    public ConditionCombineMode CombineMode { get; set; } = ConditionCombineMode.Any;

    /// <summary>Only store conditions that are actually added</summary>
    [JsonProperty("c")]
    public List<Condition> Conditions { get; set; } = [];

    public bool Evaluate(WorldState world, ConditionRegistry registry)
    {
        if (Conditions.Count == 0) return true;

        if (CombineMode == ConditionCombineMode.All)
        {
            foreach (var c in Conditions)
                if (!c.Evaluate(world, registry)) return false;
            return true;
        }

        foreach (var c in Conditions)
            if (c.Evaluate(world, registry)) return true;
        return false;
    }
}
