namespace AutoHook.Conditions.Definitions;

public sealed class StatusActiveCD : IConditionDefinition {
    public string Id => nameof(StatusActiveCD);
    public string Name => "Status";
    public string Category => "Status";
    public string Description => "Checks whether any of the selected statuses are currently active.";
    public ConditionScopeFlags AllowedScopes => ConditionScopeFlags.All;

    private readonly record struct StatusActiveParams(IReadOnlyList<uint> Ids, bool Invert) {
        public Dictionary<string, object> ToParams() {
            var dict = new Dictionary<string, object>();
            if (Ids.Count > 0)
                dict["ids"] = Ids.Select(id => (object)(long)id).ToList();
            if (Invert)
                dict["inv"] = true;
            return dict;
        }
    }

    public bool Evaluate(WorldState world, IReadOnlyDictionary<string, object> parameters) {
        var args = GetParams(parameters);
        if (args.Ids.Count == 0) return args.Invert;
        var result = args.Ids.Any(world.HasStatus);
        return args.Invert ? !result : result;
    }

    public void DrawParams(Condition condition) {
        var args = GetParams(condition.Params);
        var currentId = args.Ids.Count > 0 ? args.Ids[0] : 0;

        var statuses = typeof(IDs.Status).GetFields()
            .Select(f => f.GetValue(null))
            .OfType<uint>()
            .Where(id => id != 0)
            .Select(id => (Id: id, Name: MultiString.GetStatusName(id)))
            .Where(x => !string.IsNullOrEmpty(x.Name))
            .OrderBy(x => x.Name)
            .ToList();

        var selectedLabel = currentId != 0
            ? $"{currentId}: {MultiString.GetStatusName(currentId)}"
            : "Select status";

        DrawUtil.DrawComboSelector(
            statuses,
            s => $"{s.Id}: {s.Name}",
            selectedLabel,
            s => {
                var newArgs = args with { Ids = [s.Id] };
                condition.Params = newArgs.ToParams();
            });
    }

    private static StatusActiveParams GetParams(IReadOnlyDictionary<string, object> p) {
        var ids = IConditionDefinition.GetStatusIds(p);
        var inv = IConditionDefinition.GetBool(p, "inv", false);
        return new StatusActiveParams(ids, inv);
    }
}
