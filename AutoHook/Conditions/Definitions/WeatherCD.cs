using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Lumina.Excel.Sheets;
using static AutoHook.Conditions.IConditionDefinition;

namespace AutoHook.Conditions.Definitions;

public sealed class WeatherCD : IConditionDefinition {
    public string Id => nameof(WeatherCD);
    public string Name => "Weather";
    public ConditionScopeFlags AllowedScopes => ConditionScopeFlags.Hook | ConditionScopeFlags.FishIgnore | ConditionScopeFlags.AutoCast;

    public bool Evaluate(WorldState world, IReadOnlyDictionary<string, object> parameters) {
        var ids = GetWeatherIds(parameters);
        if (ids.Count == 0) return false;

        var slot = GetOp(parameters, "slot", "current");
        var invert = GetBool(parameters, "inv", false);

        byte targetWeatherId;

        var territorySheet = Svc.Data.GetExcelSheet<TerritoryType>();
        var weatherSheet = Svc.Data.GetExcelSheet<Weather>();

        if (world.TerritoryId == 0 || !territorySheet.TryGetRow(world.TerritoryId, out var territory))
            return invert;

        var weather = slot switch {
            "prev" => territory.GetPreviousWeather(),
            "next" => territory.GetNextWeather(),
            _ => territory.GetCurrentWeather(),
        };
        targetWeatherId = (byte)weather.RowId;

        if (!weatherSheet.TryGetRow(targetWeatherId, out var current))
            return invert;
        var currentName = current.Name.ToString();
        if (string.IsNullOrEmpty(currentName))
            return invert;

        var match = false;
        foreach (var id in ids) {
            if (weatherSheet.TryGetRow(id, out var row) && row.Name.ToString() == currentName) {
                match = true;
                break;
            }
        }
        return invert ? !match : match;
    }

    public void DrawParams(Condition condition) {
        condition.EnsureUiId();
        using var idScope = ImRaii.PushId($"weather{condition.UiId}");

        var ids = GetWeatherIds(condition.Params);
        var currentId = ids.Count > 0 ? ids[0] : (byte)0;

        var sheet = Svc.Data.GetExcelSheet<Weather>();

        var slot = condition.Params.TryGetValue("slot", out var s) ? s?.ToString() ?? "current" : "current";
        var slotLabel = slot switch {
            "prev" => UIStrings.Previous,
            "next" => UIStrings.Next,
            _ => UIStrings.Current,
        };

        ImGui.SetNextItemWidth(90 * ImGuiHelpers.GlobalScale);
        using (var comboSlot = ImRaii.Combo("##weather_slot", slotLabel)) {
            if (comboSlot.Success) {
                if (ImGui.Selectable(UIStrings.Previous, slot == "prev")) slot = "prev";
                if (ImGui.Selectable(UIStrings.Current, slot == "current")) slot = "current";
                if (ImGui.Selectable(UIStrings.Next, slot == "next")) slot = "next";
                condition.Params["slot"] = slot;
            }
        }

        ImGui.SameLine();

        var unique = new Dictionary<string, byte>();
        foreach (var row in sheet) {
            var name = row.Name.ToString();
            if (string.IsNullOrEmpty(name)) continue;
            if (!unique.ContainsKey(name))
                unique[name] = (byte)row.RowId;
        }

        var weathers = unique
            .OrderBy(k => k.Key)
            .Select(k => (Id: k.Value, Name: k.Key))
            .ToList();

        var label = currentId != 0 && sheet.TryGetRow(currentId, out var currentRow)
            ? currentRow.Name.ToString()
            : "Any weather";

        DrawUtil.DrawComboSelector(
            weathers,
            w => w.Name,
            label,
            w => {
                condition.Params["ids"] = new List<object> { (long)w.Id };
            });
    }
}
