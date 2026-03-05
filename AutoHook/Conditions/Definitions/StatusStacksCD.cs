using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using static AutoHook.Conditions.IConditionDefinition;

namespace AutoHook.Conditions.Definitions;

public sealed class StatusStacksCD : IConditionDefinition
{
    public string Id => nameof(StatusStacksCD);
    public string Name => "Status stacks";
    public string Category => "Status";
    public string Description => "Checks stacks for selected statuses against a threshold.";
    public ConditionScopeFlags AllowedScopes => ConditionScopeFlags.Hook | ConditionScopeFlags.FishIgnore | ConditionScopeFlags.AutoCast;

    public bool Evaluate(WorldState world, IReadOnlyDictionary<string, object> parameters)
    {
        var ids = GetStatusIds(parameters);
        var minStacks = GetInt(parameters, "minStacks", 1);
        if (ids.Count == 0) return false;
        var op = GetOp(parameters, "op", ">=");
        var result = ids.Any(id => CompareInt(world.GetStatusStacks(id), minStacks, op));
        return GetBool(parameters, "inv", false) ? !result : result;
    }

    public void DrawParams(Condition condition)
    {
        new StatusActiveCD().DrawParams(condition);

        ImGui.SameLine();
        var minStacks = GetInt(condition.Params, "minStacks", 1);
        ImGui.SetNextItemWidth(60 * ImGuiHelpers.GlobalScale);
        if (ImGui.InputInt("Stacks", ref minStacks))
        {
            minStacks = Math.Max(1, minStacks);
            condition.Params["minStacks"] = (long)minStacks;
        }

        ImGui.SameLine();
        var op = condition.Params.TryGetValue("op", out var o) ? o?.ToString() ?? ">=" : ">=";
        var label = op is ">" or "<" or "<=" or "=" ? op : ">=";
        ImGui.SetNextItemWidth(50 * ImGuiHelpers.GlobalScale);
        using var combo = ImRaii.Combo("##stacks_op", label);
        if (!combo) return;

        foreach (var choice in new[] { ">", ">=", "<", "<=", "=" })
        {
            var sel = choice == op;
            if (ImGui.Selectable(choice, sel))
                condition.Params["op"] = choice;
        }
    }
}
