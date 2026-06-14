using Lumina.Excel.Sheets;
using static AutoHook.Conditions.IConditionDefinition;

namespace AutoHook.Conditions.Definitions;

public sealed class OceanRouteCD : IConditionDefinition {
    public string Id => nameof(OceanRouteCD);
    public string Name => "Ocean route";
    public ConditionScopeFlags AllowedScopes => ConditionScopeFlags.Hook | ConditionScopeFlags.FishIgnore | ConditionScopeFlags.AutoCast;

    public bool Evaluate(WorldState world, IReadOnlyDictionary<string, object> parameters) {
        var ids = GetIds(parameters);
        if (ids.Count == 0) return false;
        var route = world.OceanFishing.CurrentRoute;
        var result = ids.Contains(route);
        return GetBool(parameters, "inv", false) ? !result : result;
    }

    public void DrawParams(Condition condition) {
        var ids = GetIds(condition.Params);
        var currentId = ids.Count > 0 ? ids[0] : 0;

        var unique = new Dictionary<string, uint>();
        foreach (var row in Svc.Data.GetExcelSheet<IKDRoute>()) {
            if (row.RowId == 0) continue;
            var name = row.Name.ToString();
            if (string.IsNullOrEmpty(name)) continue;
            if (!unique.ContainsKey(name))
                unique[name] = row.RowId;
        }

        var routes = unique.OrderBy(k => k.Key).Select(k => (Id: k.Value, Name: k.Key)).ToList();
        var label = currentId != 0 && Svc.Data.GetExcelSheet<IKDRoute>().TryGetRow(currentId, out var currentRow)
            ? $"{currentRow.RowId}: {currentRow.Name}"
            : "Select route";

        DrawUtil.DrawComboSelector(routes, r => $"{r.Id}: {r.Name}", label, r => { condition.Params["ids"] = new List<object> { (long)r.Id }; });
    }
}
