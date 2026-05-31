using AutoHook.Data;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using static AutoHook.Conditions.IConditionDefinition;

namespace AutoHook.Conditions.Definitions;

public sealed class OceanTimeOfDayCD : IConditionDefinition {
    public string Id => nameof(OceanTimeOfDayCD);
    public string Name => "Ocean time of day";
    public ConditionScopeFlags AllowedScopes => ConditionScopeFlags.Hook | ConditionScopeFlags.FishIgnore | ConditionScopeFlags.AutoCast;

    public bool Evaluate(WorldState world, IReadOnlyDictionary<string, object> parameters) {
        var wanted = (TimeOfDay)GetInt(parameters, "tod", 0);
        var result = world.OceanFishing.TimeOfDay == wanted;
        return GetBool(parameters, "inv", false) ? !result : result;
    }

    public void DrawParams(Condition condition) {
        var tod = (TimeOfDay)Math.Clamp(GetInt(condition.Params, "tod", 1), 1, 3);

        ImGui.SetNextItemWidth(80 * ImGuiHelpers.GlobalScale);
        var label = tod switch {
            TimeOfDay.Day => "Day",
            TimeOfDay.Sunset => "Sunset",
            TimeOfDay.Night => "Night",
            _ => "Day",
        };

        using var combo = ImRaii.Combo("Time of day", label);
        if (!combo)
            return;

        if (ImGui.Selectable("Day", tod == TimeOfDay.Day)) {
            condition.Params["tod"] = (long)TimeOfDay.Day;
        }

        if (ImGui.Selectable("Sunset", tod == TimeOfDay.Sunset)) {
            condition.Params["tod"] = (long)TimeOfDay.Sunset;
        }

        if (ImGui.Selectable("Night", tod == TimeOfDay.Night)) {
            condition.Params["tod"] = (long)TimeOfDay.Night;
        }
    }
}
