using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using static AutoHook.Conditions.IConditionDefinition;

namespace AutoHook.Conditions.Definitions;

public sealed class OceanMissionProgressCD : IConditionDefinition {
    public string Id => nameof(OceanMissionProgressCD);
    public string Name => "Ocean mission progress";
    public string Category => "Fishing";
    public string Description => "Compares progress of a selected ocean fishing mission (1–3) against a value.";
    public ConditionScopeFlags AllowedScopes => ConditionScopeFlags.Hook | ConditionScopeFlags.FishIgnore | ConditionScopeFlags.AutoCast;

    public bool Evaluate(WorldState world, IReadOnlyDictionary<string, object> parameters) {
        var slot = GetInt(parameters, "mission", 1);
        var val = GetInt(parameters, "val", 0);
        var op = GetOp(parameters, "op", ">=");
        var of = world.OceanFishing;
        var progress = slot switch {
            2 => of.Mission2Progress,
            3 => of.Mission3Progress,
            _ => of.Mission1Progress,
        };
        var result = CompareInt(progress, val, op);
        return GetBool(parameters, "inv", false) ? !result : result;
    }

    public void DrawParams(Condition condition) {
        var mission = GetInt(condition.Params, "mission", 1);
        mission = Math.Clamp(mission, 1, 3);

        ImGui.SetNextItemWidth(60 * ImGuiHelpers.GlobalScale);
        using (var combo = ImRaii.Combo("Mission", $"{mission}")) {
            if (combo.Success) {
                if (ImGui.Selectable("1", mission == 1)) { mission = 1; condition.Params["mission"] = (long)1; }
                if (ImGui.Selectable("2", mission == 2)) { mission = 2; condition.Params["mission"] = (long)2; }
                if (ImGui.Selectable("3", mission == 3)) { mission = 3; condition.Params["mission"] = (long)3; }
            }
        }

        ImGui.SameLine();
        var val = GetInt(condition.Params, "val", 0);
        ImGui.SetNextItemWidth(70 * ImGuiHelpers.GlobalScale);
        if (ImGui.InputInt("Progress", ref val)) {
            val = Math.Max(0, val);
            condition.Params["val"] = (long)val;
        }

        ImGui.SameLine();
        var op = condition.Params.TryGetValue("op", out var o) ? o?.ToString() ?? ">=" : ">=";
        var label = op is ">" or "<" or "<=" or "=" ? op : ">=";
        ImGui.SetNextItemWidth(50 * ImGuiHelpers.GlobalScale);
        using (var combo = ImRaii.Combo("##mission_op", label)) {
            if (!combo) return;

            foreach (var choice in new[] { ">", ">=", "<", "<=", "=" }) {
                var sel = choice == op;
                if (ImGui.Selectable(choice, sel))
                    condition.Params["op"] = choice;
            }
        }
    }
}
