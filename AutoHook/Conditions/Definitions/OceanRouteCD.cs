using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Lumina.Excel.Sheets;
using static AutoHook.Conditions.IConditionDefinition;

namespace AutoHook.Conditions.Definitions;

public sealed class OceanRouteCD : IConditionDefinition
{
    public string Id => nameof(OceanRouteCD);
    public string Name => "Ocean route";
    public string Category => "Fishing";
    public string Description => "Matches the current ocean fishing route against selected routes.";
    public ConditionScopeFlags AllowedScopes => ConditionScopeFlags.Hook | ConditionScopeFlags.FishIgnore | ConditionScopeFlags.AutoCast;

    public bool Evaluate(WorldState world, IReadOnlyDictionary<string, object> parameters)
    {
        var ids = GetIds(parameters);
        if (ids.Count == 0) return false;
        var route = world.OceanFishing.CurrentRoute;
        var result = ids.Contains(route);
        return GetBool(parameters, "inv", false) ? !result : result;
    }

    public void DrawParams(Condition condition)
    {
        var ids = GetIds(condition.Params);
        var currentId = ids.Count > 0 ? ids[0] : 0;

        ImGui.SetNextItemWidth(220 * ImGuiHelpers.GlobalScale);
        var sheet = Svc.Data.GetExcelSheet<IKDRoute>();
        if (sheet == null)
        {
            DrawIdsParams(condition, "Route IDs");
            return;
        }

        var unique = new Dictionary<string, uint>();
        foreach (var row in sheet)
        {
            if (row.RowId == 0) continue;
            var name = row.Name.ToString();
            if (string.IsNullOrEmpty(name)) continue;
            if (!unique.ContainsKey(name))
                unique[name] = row.RowId;
        }

        var label = currentId != 0 && sheet.TryGetRow(currentId, out var currentRow) ? $"{currentRow.RowId}: {currentRow.Name}" : "Select route";
        using var combo = ImRaii.Combo("Route", label);
        if (!combo) return;

        foreach (var kv in unique.OrderBy(k => k.Key))
        {
            var id = kv.Value;
            var name = kv.Key;
            var sel = id == currentId;
            if (!ImGui.Selectable($"{id}: {name}", sel))
                continue;

            currentId = id;
            condition.Params["ids"] = new List<object> { (long)id };
        }
    }
}
