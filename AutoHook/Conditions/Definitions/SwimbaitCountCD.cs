using AutoHook.Utils;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using static AutoHook.Conditions.IConditionDefinition;

namespace AutoHook.Conditions.Definitions;

public sealed class SwimbaitCountCD : IConditionDefinition {
    public string Id => nameof(SwimbaitCountCD);
    public string Name => "Swimbait count";
    public string Category => "Fishing";
    public string Description => "Compares swimbait count (total or for a fish) against a value.";
    public ConditionScopeFlags AllowedScopes => ConditionScopeFlags.Hook | ConditionScopeFlags.AutoCast;

    public bool Evaluate(WorldState world, IReadOnlyDictionary<string, object> parameters) {
        var val = GetInt(parameters, "val", 0);
        var op = ResolveOp(parameters);
        var fishId = GetInt(parameters, "id", 0);
        if (fishId == 0 && world.SwimbaitEvaluationFishId != 0)
            fishId = (int)world.SwimbaitEvaluationFishId;

        var count = fishId > 0
            ? world.GetSwimbaitCountForFish((uint)fishId)
            : world.GetSwimbaitCount();
        var result = CompareInt(count, val, op);
        return GetBool(parameters, "inv", false) ? !result : result;
    }

    public void DrawParams(Condition condition) {
        var fishId = GetInt(condition.Params, "id", 0);
        var val = GetInt(condition.Params, "val", 0);

        var selectedName = fishId switch {
            0 => "Current slot fish",
            > 0 when GameRes.Fishes.FirstOrDefault(f => f.Id == fishId) is { } fish => $"[#{fish.Id}] {fish.Name}",
            _ => "-",
        };

        var fishOptions = new List<BaitFishClass> { new("Current slot fish", 0) };
        fishOptions.AddRange(GameRes.Fishes);
        DrawUtil.DrawComboSelector(
            fishOptions,
            fish => fish.Id == 0 ? fish.Name : $"[#{fish.Id}] {fish.Name}",
            selectedName,
            fish => condition.Params["id"] = (long)fish.Id);

        ImGui.SameLine();
        ImGui.SetNextItemWidth(80 * ImGuiHelpers.GlobalScale);
        if (ImGui.InputInt("Swimbaits", ref val))
            condition.Params["val"] = (long)val;

        ImGui.SameLine();
        var op = condition.Params.TryGetValue("op", out var o) ? o?.ToString() ?? ">=" : ">=";
        var label = op is ">" or "<" or "<=" or "=" ? op : ">=";
        ImGui.SetNextItemWidth(50 * ImGuiHelpers.GlobalScale);
        using var combo = ImRaii.Combo("##swimbait_op", label);
        if (!combo) return;

        foreach (var choice in new[] { ">", ">=", "<", "<=", "=" }) {
            var sel = choice == op;
            if (ImGui.Selectable(choice, sel))
                condition.Params["op"] = choice;
        }
    }

    private static string ResolveOp(IReadOnlyDictionary<string, object> parameters) {
        if (parameters.TryGetValue("op", out var opObj) && opObj != null)
            return opObj.ToString() ?? ">=";
        if (parameters.TryGetValue("above", out var aboveObj)) {
            var above = aboveObj is bool b ? b : Convert.ToInt32(aboveObj) != 0;
            return above ? ">=" : "<=";
        }

        return ">=";
    }
}
