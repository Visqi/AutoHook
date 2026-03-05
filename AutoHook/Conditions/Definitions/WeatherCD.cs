using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Lumina.Excel.Sheets;
using static AutoHook.Conditions.IConditionDefinition;

namespace AutoHook.Conditions.Definitions;

public sealed class WeatherCD : IConditionDefinition
{
    public string Id => nameof(WeatherCD);
    public string Name => "Weather";
    public string Category => "World";
    public string Description => "Checks current weather (by name) against one or more weather entries.";
    public ConditionScopeFlags AllowedScopes => ConditionScopeFlags.Hook | ConditionScopeFlags.FishIgnore | ConditionScopeFlags.AutoCast;

    public bool Evaluate(WorldState world, IReadOnlyDictionary<string, object> parameters)
    {
        var ids = GetWeatherIds(parameters);
        if (ids.Count == 0) return false;

        var sheet = Svc.Data.GetExcelSheet<Weather>();
        if (sheet == null)
        {
            var result = ids.Contains(world.CurrentWeatherId);
            return GetBool(parameters, "inv", false) ? !result : result;
        }

        if (!sheet.TryGetRow(world.CurrentWeatherId, out var current))
            return GetBool(parameters, "inv", false);
        var currentName = current.Name.ToString();
        if (string.IsNullOrEmpty(currentName))
            return GetBool(parameters, "inv", false);

        var match = false;
        foreach (var id in ids)
        {
            if (sheet.TryGetRow(id, out var row) && row.Name.ToString() == currentName)
            {
                match = true;
                break;
            }
        }
        return GetBool(parameters, "inv", false) ? !match : match;
    }

    public void DrawParams(Condition condition)
    {
        var ids = GetWeatherIds(condition.Params);
        var currentId = ids.Count > 0 ? ids[0] : (byte)0;

        ImGui.SetNextItemWidth(180 * ImGuiHelpers.GlobalScale);
        var sheet = Svc.Data.GetExcelSheet<Weather>();
        if (sheet == null)
        {
            DrawIdsParams(condition, "Weather IDs");
            return;
        }

        var unique = new Dictionary<string, byte>();
        foreach (var row in sheet)
        {
            var name = row.Name.ToString();
            if (string.IsNullOrEmpty(name)) continue;
            if (!unique.ContainsKey(name))
                unique[name] = (byte)row.RowId;
        }

        var label = currentId != 0 && sheet.TryGetRow(currentId, out var currentRow) ? currentRow.Name.ToString() : "Any weather";
        using var combo = ImRaii.Combo("Weather", label);
        if (!combo) return;

        foreach (var kv in unique.OrderBy(k => k.Key))
        {
            var id = kv.Value;
            var name = kv.Key;
            var sel = id == currentId;
            if (!ImGui.Selectable(name, sel))
                continue;

            currentId = id;
            condition.Params["ids"] = new List<object> { (long)id };
        }
    }
}
