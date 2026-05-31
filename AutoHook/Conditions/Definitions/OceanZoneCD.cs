using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using static AutoHook.Conditions.IConditionDefinition;

namespace AutoHook.Conditions.Definitions;

public sealed class OceanZoneCD : IConditionDefinition {
    public string Id => nameof(OceanZoneCD);
    public string Name => "Ocean zone";
    public ConditionScopeFlags AllowedScopes => ConditionScopeFlags.Hook | ConditionScopeFlags.FishIgnore | ConditionScopeFlags.AutoCast;

    public bool Evaluate(WorldState world, IReadOnlyDictionary<string, object> parameters) {
        var wanted = GetInt(parameters, "zone", 0);
        var zone = (int)world.OceanFishing.CurrentZone;
        var result = zone == wanted;
        return GetBool(parameters, "inv", false) ? !result : result;
    }

    public void DrawParams(Condition condition) {
        var zone = GetInt(condition.Params, "zone", 0);
        zone = Math.Clamp(zone, 0, 2);

        ImGui.SetNextItemWidth(60 * ImGuiHelpers.GlobalScale);
        var label = zone switch {
            0 => "1",
            1 => "2",
            2 => "3",
            _ => $"{zone}",
        };

        using var combo = ImRaii.Combo("Zone", label);
        if (!combo)
            return;

        if (ImGui.Selectable("Zone 1", zone == 0)) { zone = 0; condition.Params["zone"] = (long)0; }
        if (ImGui.Selectable("Zone 2", zone == 1)) { zone = 1; condition.Params["zone"] = (long)1; }
        if (ImGui.Selectable("Zone 3", zone == 2)) { zone = 2; condition.Params["zone"] = (long)2; }
    }
}
