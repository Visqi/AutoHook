using Dalamud.Bindings.ImGui;
using static AutoHook.Conditions.IConditionDefinition;

namespace AutoHook.Conditions.Definitions;

public sealed class StatusStacksCD : IConditionDefinition {
    public string Id => nameof(StatusStacksCD);
    public string Name => "Status stacks";
    public string Category => "Status";
    public string Description => "Checks stacks for selected statuses against a threshold.";
    public ConditionScopeFlags AllowedScopes => ConditionScopeFlags.Hook | ConditionScopeFlags.FishIgnore | ConditionScopeFlags.AutoCast;

    public bool Evaluate(WorldState world, IReadOnlyDictionary<string, object> parameters) {
        var ids = GetStatusIds(parameters);
        var args = GetIntCompareParams(parameters, "minStacks", 1);
        if (ids.Count == 0) return false;
        var result = ids.Any(id => CompareInt(world.GetStatusStacks(id), args.Value, args.Op));
        return args.Invert ? !result : result;
    }

    public void DrawParams(Condition condition) {
        new StatusActiveCD().DrawParams(condition);

        ImGui.SameLine();
        DrawIntCompareParams(condition, "##stacks_op", "Stacks", valueKey: "minStacks", defaultValue: 1, clamp: v => Math.Max(1, v), valueWidth: 60);
    }
}
