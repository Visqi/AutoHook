using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Colors;
using static AutoHook.Conditions.IConditionDefinition;

namespace AutoHook.Conditions;

public static class PresetConditionHelper {
    public const string TypeIdPrefix = "preset:";

    [ThreadStatic]
    private static HashSet<Guid>? _evalStack;

    public static string ToTypeId(Guid id) => TypeIdPrefix + id.ToString("N");

    public static bool TryParseTypeId(string typeId, out Guid id) {
        if (typeId.StartsWith(TypeIdPrefix, StringComparison.Ordinal)
            && Guid.TryParse(typeId.AsSpan(TypeIdPrefix.Length), out id))
            return true;

        id = default;
        return false;
    }

    public static bool IsPresetType(string typeId) => TryParseTypeId(typeId, out _);

    public static CustomPresetConfig? GetEvaluationPreset()
        => Ui.ConditionUi.EvaluationPreset ?? Service.Configuration.HookPresets.CurrentPreset;

    public static NamedConditionConfig? FindNamed(CustomPresetConfig preset, Guid id)
        => preset.NamedConditions.FirstOrDefault(n => n.UniqueId == id);

    public static string? ResolveDisplayName(string typeId, CustomPresetConfig? preset) {
        if (preset == null || !TryParseTypeId(typeId, out var id))
            return null;

        var named = FindNamed(preset, id);
        if (named == null)
            return UIStrings.PresetConditions_Missing;

        return string.IsNullOrWhiteSpace(named.Name) ? UIStrings.PresetConditions_NewName : named.Name;
    }

    public static IReadOnlyList<ConditionTypeDef> GetPresetTypeDefs(CustomPresetConfig preset, Guid? excludeId = null) {
        var defs = new List<ConditionTypeDef>();
        foreach (var named in preset.NamedConditions) {
            if (excludeId.HasValue && named.UniqueId == excludeId.Value)
                continue;
            if (string.IsNullOrWhiteSpace(named.Name))
                continue;

            var id = named.UniqueId;
            defs.Add(new ConditionTypeDef {
                Id = ToTypeId(id),
                Name = named.Name,
                AllowedScopes = ConditionScopeFlags.All,
                Evaluate = (world, _) => EvaluateNamed(id, world),
                DrawParams = DrawDefinedInTabHint,
            });
        }

        return defs;
    }

    public static bool EvaluateFromTypeId(string typeId, WorldState world, IReadOnlyDictionary<string, object> parameters) {
        if (!TryParseTypeId(typeId, out var id))
            return false;

        var result = EvaluateNamed(id, world);
        return GetBool(parameters, "inv", false) ? !result : result;
    }

    public static bool EvaluateNamed(Guid id, WorldState world) {
        var preset = GetEvaluationPreset();
        if (preset == null)
            return false;

        _evalStack ??= [];
        if (!_evalStack.Add(id))
            return false;

        try {
            var named = FindNamed(preset, id);
            return named != null && named.ConditionSet.Evaluate(world, ConditionRegistry.Registry);
        }
        finally {
            _evalStack.Remove(id);
        }
    }

    private static void DrawDefinedInTabHint(Condition _) {
        ImGui.TextColored(ImGuiColors.DalamudGrey, UIStrings.PresetConditions_DefinedInTab);
    }
}
