using System.Diagnostics.CodeAnalysis;

namespace AutoHook.Conditions.Model;

public static class ConditionSetUtil {
    public static bool HasGroups([NotNullWhen(true)] this ConditionSet? set)
        => set is { Groups.Count: > 0 };

    // any group with at least one condition.
    public static bool HasAnyCondition([NotNullWhen(true)] this ConditionSet? set)
        => set is { Groups.Count: > 0 } && set.Groups.Any(g => g.Conditions.Count > 0);

    // null/empty = pass. otherwise evaluate.
    public static bool PassesOrUnconfigured(this ConditionSet? set, WorldState ws)
        => set is not { Groups.Count: > 0 } || set.Evaluate(ws, Registry);

    // has groups and eval passes. unconfigured = false.
    public static bool Passes([NotNullWhen(true)] this ConditionSet? set, WorldState ws)
        => set is { Groups.Count: > 0 } && set.Evaluate(ws, Registry);

    // has groups and eval fails. unconfigured = false.
    public static bool Fails([NotNullWhen(true)] this ConditionSet? set, WorldState ws)
        => set is { Groups.Count: > 0 } && !set.Evaluate(ws, Registry);

    // --- single-condition binding (typed SingleCondition facade) ---

    public static Condition? GetFirstCondition(this ConditionSet? set, string typeId) {
        if (!set.HasGroups())
            return null;
        foreach (var group in set.Groups) {
            var c = group.Conditions.FirstOrDefault(x => x.TypeId == typeId);
            if (c != null)
                return c;
        }
        return null;
    }

    public static ConditionSet? SetSingleCondition(ConditionSet? current, string typeId, IReadOnlyDictionary<string, object>? conditionParams) {
        if (conditionParams == null || conditionParams.Count == 0) {
            if (!current.HasGroups())
                return current;
            foreach (var group in current.Groups)
                group.Conditions.RemoveAll(c => c.TypeId == typeId);
            return current;
        }

        var set = current ?? new ConditionSet { CombineMode = ConditionCombineMode.All };
        var grp = set.HasGroups() ? set.Groups[0] : null;
        if (grp == null) {
            grp = new ConditionGroup { CombineMode = ConditionCombineMode.All };
            set.Groups.Add(grp);
        }

        var cond = grp.Conditions.FirstOrDefault(c => c.TypeId == typeId);
        var paramsDict = new Dictionary<string, object>(conditionParams);
        if (cond == null)
            grp.Conditions.Add(new Condition { TypeId = typeId, Params = paramsDict });
        else
            cond.Params = paramsDict;
        return set;
    }

    // drop empty groups. null if nothing left.
    public static ConditionSet? CompactOrNull(ConditionSet? set) {
        if (!set.HasGroups())
            return null;
        set.Groups.RemoveAll(g => g.Conditions.Count == 0);
        return set.HasGroups() ? set : null;
    }

    // --- overcap: empty ≠ allow (unlike PassesOrUnconfigured) ---

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

    // true = cordial may overcap GP; false = default GP math.
    public static bool EvaluateAllowsOvercap(ConditionSet? set, WorldState world)
        => HasAnyEnabledCondition(set) && set!.Evaluate(world, Registry);
}
