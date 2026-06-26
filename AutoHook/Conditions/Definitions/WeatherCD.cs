using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Lumina.Excel.Sheets;
using static AutoHook.Conditions.IConditionDefinition;

namespace AutoHook.Conditions.Definitions;

public sealed class WeatherCD : SnapshottableConditionDefinition {
    public override string Id => nameof(WeatherCD);
    public override string Name => "Weather";
    public override ConditionScopeFlags AllowedScopes => ConditionScopeFlags.Hook | ConditionScopeFlags.FishIgnore | ConditionScopeFlags.AutoCast;

    protected override bool EvaluateLive(WorldState world, IReadOnlyDictionary<string, object> parameters) {
        var ids = GetWeatherIds(parameters);
        if (ids.Count == 0) return false;

        var slot = GetOp(parameters, "slot", "current");
        var invert = GetBool(parameters, "inv", false);

        var weatherId = slot switch {
            "prev" => world.PreviousWeatherId,
            "next" => world.NextWeatherId,
            _ => world.CurrentWeatherId,
        };
        return EvaluateWeather(ids, weatherId, invert);
    }

    protected override bool EvaluateSnapshot(CastInfoSnapshot snapshot, IReadOnlyDictionary<string, object> parameters) {
        var ids = GetWeatherIds(parameters);
        if (ids.Count == 0) return false;

        var slot = GetOp(parameters, "slot", "current");
        var invert = GetBool(parameters, "inv", false);
        var targetWeatherId = slot switch {
            "prev" => snapshot.PreviousWeatherId,
            "next" => snapshot.NextWeatherId,
            _ => snapshot.CurrentWeatherId,
        };
        return EvaluateWeather(ids, targetWeatherId, invert);
    }

    private static bool EvaluateWeather(List<byte> ids, byte targetWeatherId, bool invert) {
        if (targetWeatherId == 0)
            return invert;

        if (!Weather.TryGetRow(targetWeatherId, out var current))
            return invert;
        var currentName = current.Name.ToString();
        if (string.IsNullOrEmpty(currentName))
            return invert;

        var match = ids.Any(id => Weather.TryGetRow(id, out var row) && row.Name.ToString() == currentName);
        return invert ? !match : match;
    }

    public override void DrawParams(Condition condition) {
        condition.EnsureUiId();
        using var idScope = ImRaii.PushId($"weather{condition.UiId}");

        var ids = GetWeatherIds(condition.Params);
        var currentId = ids.Count > 0 ? ids[0] : (byte)0;

        var slot = condition.Params.TryGetValue("slot", out var s) ? s?.ToString() ?? "current" : "current";
        var slotLabel = slot switch {
            "prev" => UIStrings.Previous,
            "next" => UIStrings.Next,
            _ => UIStrings.Current,
        };

        ImGui.SetNextItemWidth(90.Scaled());
        using (var comboSlot = ImRaii.Combo("##weather_slot", slotLabel)) {
            if (comboSlot.Success) {
                if (ImGui.Selectable(UIStrings.Previous, slot == "prev")) slot = "prev";
                if (ImGui.Selectable(UIStrings.Current, slot == "current")) slot = "current";
                if (ImGui.Selectable(UIStrings.Next, slot == "next")) slot = "next";
                condition.Params["slot"] = slot;
            }
        }

        ImGui.SameLine();

        var unique = Weather.Select(row => (Name: row.Name.ToString(), Id: (byte)row.RowId)).Where(x => !string.IsNullOrEmpty(x.Name)).DistinctBy(x => x.Name).ToDictionary(x => x.Name, x => x.Id);
        var weathers = unique.OrderBy(k => k.Key).Select(k => (Id: k.Value, Name: k.Key)).ToList();
        var label = currentId != 0 && Weather.TryGetRow(currentId, out var currentRow) ? currentRow.Name.ToString() : "Any weather";
        DrawUtil.DrawComboSelector(
            weathers,
            w => w.Name,
            label,
            w => {
                condition.Params["ids"] = new List<object> { (long)w.Id };
            });
    }

    public string DescribeParameters(IReadOnlyDictionary<string, object> parameters)
        => ConditionParameterFormat.FormatWeather(parameters);
}
