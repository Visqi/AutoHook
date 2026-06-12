using System.Diagnostics.CodeAnalysis;

namespace AutoHook.Conditions;

public static class ConditionSetExtensions {
    /// <summary>True when the set has at least one group.</summary>
    public static bool HasGroups([NotNullWhen(true)] this ConditionSet? set)
        => set is { Groups.Count: > 0 };

    /// <summary>True when at least one group contains at least one condition.</summary>
    public static bool HasAnyCondition([NotNullWhen(true)] this ConditionSet? set)
        => set is { Groups.Count: > 0 } && set.Groups.Any(g => g.Conditions.Count > 0);

    /// <summary>
    /// Null or no groups: passes. Otherwise evaluates.
    /// Matches the common "no conditions configured = allow" pattern.
    /// </summary>
    public static bool PassesOrUnconfigured(this ConditionSet? set)
        => set is not { Groups.Count: > 0 } || set.Evaluate(Service.WorldState, ConditionRegistry.Registry);

    /// <summary>Has groups and evaluation passes. Unconfigured sets return false.</summary>
    public static bool Passes([NotNullWhen(true)] this ConditionSet? set)
        => set is { Groups.Count: > 0 } && set.Evaluate(Service.WorldState, ConditionRegistry.Registry);

    /// <summary>Has groups and evaluation fails. Unconfigured sets return false.</summary>
    public static bool Fails([NotNullWhen(true)] this ConditionSet? set)
        => set is { Groups.Count: > 0 } && !set.Evaluate(Service.WorldState, ConditionRegistry.Registry);
}
