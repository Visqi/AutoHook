using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using static AutoHook.Conditions.IConditionDefinition;

namespace AutoHook.Conditions.Definitions;

public sealed class FishCountCD : IConditionDefinition {
    public string Id => nameof(FishCountCD);
    public string Name => "Fish count";
    public string Category => "Counters";
    public string Description => "Compares the session fish counter for a specific fish against a value.";
    public ConditionScopeFlags AllowedScopes => ConditionScopeFlags.Hook | ConditionScopeFlags.FishIgnore | ConditionScopeFlags.AutoCast;

    public bool Evaluate(WorldState world, IReadOnlyDictionary<string, object> parameters) {
        var fishId = GetInt(parameters, "id", 0);
        var val = GetInt(parameters, "val", 0);
        var op = GetOp(parameters, "op", ">=");
        var invert = GetBool(parameters, "inv", false);

        if (fishId <= 0)
            return invert;

        // Sum helper counters for all FishConfig entries matching this fish across all presets
        var presets = Service.Configuration.HookPresets.CustomPresets.Append(Service.Configuration.HookPresets.DefaultPreset);
        var total = presets
            .SelectMany(p => p.ListOfFish)
            .Where(f => f.Fish.Id == fishId)
            .Sum(f => FishingManager.FishingHelper.GetFishCount(f.UniqueId));

        var result = CompareInt(total, val, op);
        return invert ? !result : result;
    }

    public void DrawParams(Condition condition) {
        var fishId = GetInt(condition.Params, "id", 0);
        var val = GetInt(condition.Params, "val", 0);

        var currentFish = GameRes.Fishes.FirstOrDefault(f => f.Id == fishId);
        var selectedName = currentFish is { Id: > 0 }
            ? $"[#{currentFish.Id}] {currentFish.Name}"
            : "-";

        DrawUtil.DrawComboSelector(
            GameRes.Fishes,
            fish => $"[#{fish.Id}] {fish.Name}",
            selectedName,
            fish => {
                condition.Params["id"] = (long)fish.Id;
            });

        ImGui.SameLine();
        ImGui.SetNextItemWidth(80 * ImGuiHelpers.GlobalScale);
        if (ImGui.InputInt("Count", ref val))
            condition.Params["val"] = (long)Math.Max(0, val);

        ImGui.SameLine();
        var op = condition.Params.TryGetValue("op", out var o) ? o?.ToString() ?? ">=" : ">=";
        var label = op is ">" or "<" or "<=" or "=" ? op : ">=";
        ImGui.SetNextItemWidth(50 * ImGuiHelpers.GlobalScale);
        using var combo = ImRaii.Combo("##fishcount_op", label);
        if (!combo) return;

        foreach (var choice in new[] { ">", ">=", "<", "<=", "=" }) {
            var sel = choice == op;
            if (ImGui.Selectable(choice, sel))
                condition.Params["op"] = choice;
        }
    }
}

