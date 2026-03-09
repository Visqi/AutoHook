using Newtonsoft.Json;

namespace AutoHook.Conditions;

/// <summary>
/// A group of conditions combined with AND or OR. (X or Y) in "(X or Y) AND (A or B)".
/// </summary>
public class ConditionGroup {
    [JsonProperty("m")]
    public ConditionCombineMode CombineMode { get; set; } = ConditionCombineMode.All;

    [JsonProperty("c")]
    public List<Condition> Conditions { get; set; } = [];

    /// <summary>When false, this group is skipped in evaluation (UI: toggle without deleting).</summary>
    [JsonProperty("a")]
    public bool Enabled { get; set; } = true;

    public bool Evaluate(WorldState world, ConditionRegistry registry) {
        if (!Enabled) return true;
        var active = Conditions.Where(c => c.Enabled).ToList();
        if (active.Count == 0) return true;

        if (CombineMode == ConditionCombineMode.All)
            return active.All(c => c.Evaluate(world, registry));
        return active.Any(c => c.Evaluate(world, registry));
    }
}
