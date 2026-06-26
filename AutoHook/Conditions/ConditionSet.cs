using Newtonsoft.Json;

namespace AutoHook.Conditions;

/// <summary>
/// Top-level condition set: groups combined with AND or OR. (X or Y) AND (A or B) = two groups, top-level AND.
/// Empty groups list = no conditions = evaluate to true.
/// </summary>
public class ConditionSet {
    /// <summary>How to combine groups: All = AND, Any = OR.</summary>
    [JsonProperty("m")]
    public ConditionCombineMode CombineMode { get; set; } = ConditionCombineMode.All;

    /// <summary>Only store non-empty groups (minimal config).</summary>
    [JsonProperty("g")]
    public List<ConditionGroup> Groups { get; set; } = [];

    /// <summary>
    /// Optional boolean expression over groups (A, B, C...) using &&, || and parentheses.
    /// Example: "A && B && (C || D)". When set, this overrides CombineMode for evaluation.
    /// </summary>
    [JsonProperty("e")]
    public string? Expression { get; set; }

    /// <summary>UI-only: current expression selection start (token index).</summary>
    [JsonIgnore]
    public int? ExprSelectionStart { get; set; }

    /// <summary>UI-only: current expression selection end (token index).</summary>
    [JsonIgnore]
    public int? ExprSelectionEnd { get; set; }

    /// <summary>UI-only: whether advanced expression editor is expanded.</summary>
    [JsonIgnore]
    public bool ExprVisible { get; set; }

    /// <summary>UI-only: slim editor "Advanced" section expanded (DrawCheckboxTree style).</summary>
    [JsonIgnore]
    public bool SlimAdvancedExpanded { get; set; }

    public bool Evaluate(WorldState world, ConditionRegistry registry) {
        if (Groups.Count == 0) return true;

        // Evaluate each group once (disabled groups: true for AND, false for OR so they don't affect result)
        var values = new bool[Groups.Count];
        for (var i = 0; i < Groups.Count; i++)
            values[i] = !Groups[i].Enabled
                ? (CombineMode == ConditionCombineMode.All)
                : Groups[i].Evaluate(world, registry);

        // If an expression is provided, try to use it first
        if (!string.IsNullOrWhiteSpace(Expression)) {
            try {
                if (ConditionExpression.TryEvaluate(Expression, values, out var result))
                    return result;
            }
            catch {
                // Fallback to CombineMode
            }
        }

        if (CombineMode == ConditionCombineMode.Any) {
            foreach (var v in values)
                if (v) return true;
            return false;
        }

        foreach (var v in values)
            if (!v) return false;
        return true;
    }

    public (bool Result, List<(string Id, bool Result)> Trace) EvaluateWithTrace(WorldState world, ConditionRegistry registry) {
        if (Groups.Count == 0)
            return (true, []);

        var values = new bool[Groups.Count];
        var trace = new List<(string, bool)>();
        for (var i = 0; i < Groups.Count; i++) {
            if (!Groups[i].Enabled) {
                values[i] = CombineMode == ConditionCombineMode.All;
                continue;
            }

            var (groupResult, groupTrace) = Groups[i].EvaluateWithTrace(world, registry);
            values[i] = groupResult;
            trace.AddRange(groupTrace.Select(t => ($"G{i}:{t.Id}", t.Result)));
        }

        if (!string.IsNullOrWhiteSpace(Expression)) {
            try {
                if (ConditionExpression.TryEvaluate(Expression, values, out var result))
                    return (result, trace);
            }
            catch { }
        }

        if (CombineMode == ConditionCombineMode.Any) {
            foreach (var v in values)
                if (v) return (true, trace);
            return (false, trace);
        }

        foreach (var v in values)
            if (!v) return (false, trace);
        return (true, trace);
    }

    public List<(string Label, bool Result)> DescribeEvaluation(WorldState world, ConditionRegistry registry) {
        if (Groups.Count == 0)
            return [];

        var values = new bool[Groups.Count];
        var trace = new List<(string, bool)>();
        for (var i = 0; i < Groups.Count; i++) {
            if (!Groups[i].Enabled) {
                values[i] = CombineMode == ConditionCombineMode.All;
                continue;
            }

            var (groupResult, groupTrace) = Groups[i].DescribeWithTrace(world, registry);
            values[i] = groupResult;
            trace.AddRange(groupTrace.Select(t => ($"G{i}: {t.Label}", t.Result)));
        }

        if (!string.IsNullOrWhiteSpace(Expression)) {
            try {
                if (ConditionExpression.TryEvaluate(Expression, values, out var result))
                    return trace;
            }
            catch { }
        }

        return trace;
    }
}
