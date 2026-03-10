using Lumina.Excel.Sheets;
using static AutoHook.Conditions.IConditionDefinition;

namespace AutoHook.Conditions.Definitions;

public sealed class OceanMissionTypeCD : IConditionDefinition {
    public string Id => nameof(OceanMissionTypeCD);
    public string Name => "Ocean mission type";
    public string Category => "Fishing";
    public string Description => "Matches current ocean fishing mission types (slots 1–3) against selected entries.";
    public ConditionScopeFlags AllowedScopes => ConditionScopeFlags.Hook | ConditionScopeFlags.FishIgnore | ConditionScopeFlags.AutoCast;

    public bool Evaluate(WorldState world, IReadOnlyDictionary<string, object> parameters) {
        var ids = GetIds(parameters);
        if (ids.Count == 0) return false;
        var of = world.OceanFishing;
        var result = ids.Contains(of.Mission1Type) || ids.Contains(of.Mission2Type) || ids.Contains(of.Mission3Type);
        return GetBool(parameters, "inv", false) ? !result : result;
    }

    public void DrawParams(Condition condition) {
        var ids = GetIds(condition.Params);
        var currentId = ids.Count > 0 ? ids[0] : 0;

        var sheet = Svc.Data.GetExcelSheet<IKDPlayerMissionCondition>();
        if (sheet == null) {
            DrawIdsParams(condition, "Mission type IDs");
            return;
        }

        string LabelForRow(uint rowId) {
            if (rowId == 0) return "Select mission";
            if (!sheet.TryGetRow(rowId, out var row)) return $"{rowId}";
            var name = MultiString.ParseSeString(row.Unknown0);
            return string.IsNullOrEmpty(name) ? $"{rowId}" : $"{rowId}: {name}";
        }

        var missions = sheet
            .Where(row => !string.IsNullOrEmpty(MultiString.ParseSeString(row.Unknown0)))
            .Select(row => (Id: row.RowId, Name: MultiString.ParseSeString(row.Unknown0)))
            .OrderBy(x => x.Name)
            .ToList();

        var label = LabelForRow(currentId);

        DrawUtil.DrawComboSelector(
            missions,
            m => $"{m.Id}: {m.Name}",
            label,
            m => {
                condition.Params["ids"] = new List<object> { (long)m.Id };
            });
    }
}
