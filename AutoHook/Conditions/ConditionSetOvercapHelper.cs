using AutoHook.Data;

namespace AutoHook.Conditions;

/// <summary>
/// Overcap overrides use <see cref="ConditionSet"/> storage but must not treat "no enabled conditions"
/// as "allow overcap" (unlike normal condition sets where empty groups evaluate true).
/// </summary>
public static class ConditionSetOvercapHelper {
    public static bool HasAnyEnabledCondition(ConditionSet? set) {
        if (set?.Groups is not { Count: > 0 } groups)
            return false;

        foreach (var group in groups) {
            if (!group.Enabled)
                continue;
            foreach (var c in group.Conditions) {
                if (c.Enabled)
                    return true;
            }
        }

        return false;
    }

    /// <summary>
    /// When true, cordial may overcap GP. When false, fall back to default GP math.
    /// </summary>
    public static bool EvaluateAllowsOvercap(ConditionSet? set, WorldState world)
        => HasAnyEnabledCondition(set) && set!.Evaluate(world, ConditionRegistry.Registry);
}
