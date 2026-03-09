using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using FFXIVClientStructs.FFXIV.Client.Game;
using static AutoHook.Conditions.IConditionDefinition;

namespace AutoHook.Conditions.Definitions;

public sealed class ActionAvailableCD : IConditionDefinition {
    public string Id => nameof(ActionAvailableCD);
    public string Name => "Action";
    public string Category => "Player";
    public string Description => "Checks whether an action/item/event action is currently usable.";
    public ConditionScopeFlags AllowedScopes => ConditionScopeFlags.Hook | ConditionScopeFlags.AutoCast;

    public bool Evaluate(WorldState world, IReadOnlyDictionary<string, object> parameters) {
        var id = GetUInt(parameters, "id", 0);
        if (id == 0) return false;
        var type = (ActionType)GetInt(parameters, "type", 0);
        var result = world.ActionAvailable(id, type);
        return GetBool(parameters, "inv", false) ? !result : result;
    }

    public void DrawParams(Condition condition) {
        var id = (int)GetUInt(condition.Params, "id", 0);
        ImGui.SetNextItemWidth(80 * ImGuiHelpers.GlobalScale);
        if (ImGui.InputInt("Action ID", ref id))
            condition.Params["id"] = (long)id;

        ImGui.SameLine();
        var type = GetInt(condition.Params, "type", 0);
        var label = type switch {
            1 => "Item",
            2 => "Event",
            _ => "Action"
        };

        ImGui.SameLine();
        ImGui.SetNextItemWidth(90 * ImGuiHelpers.GlobalScale);
        using (var combo = ImRaii.Combo("##act_type", label)) {
            if (combo.Success) {
                if (ImGui.Selectable("Action", type == 0)) type = 0;
                if (ImGui.Selectable("Item", type == 1)) type = 1;
                if (ImGui.Selectable("Event", type == 2)) type = 2;
            }
        }

        condition.Params["type"] = (long)type;
    }
}
