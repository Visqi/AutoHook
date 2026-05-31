using Dalamud.Bindings.ImGui;
using static AutoHook.Conditions.IConditionDefinition;

namespace AutoHook.Conditions.Definitions;

public sealed class BaitCountCD : IConditionDefinition {
    public string Id => nameof(BaitCountCD);
    public string Name => "Bait count";
    public string Category => "Counters";
    public string Description => "Compares the inventory count for a specific bait against a value.";
    public ConditionScopeFlags AllowedScopes => ConditionScopeFlags.Hook | ConditionScopeFlags.FishIgnore | ConditionScopeFlags.AutoCast;

    public bool Evaluate(WorldState world, IReadOnlyDictionary<string, object> parameters) {
        var baitId = GetInt(parameters, "id", 0);
        var args = GetIntCompareParams(parameters);
        if (baitId <= 0)
            return args.Invert;

        var result = CompareInt(world.GetItemCount((uint)baitId), args.Value, args.Op);
        return args.Invert ? !result : result;
    }

    public void DrawParams(Condition condition) {
        var baitId = GetInt(condition.Params, "id", 0);
        var currentBait = GameRes.Baits.FirstOrDefault(b => b.Id == baitId);
        var selectedName = currentBait is { Id: > 0 }
            ? $"[#{currentBait.Id}] {currentBait.Name}"
            : "-";

        DrawUtil.DrawComboSelector(
            GameRes.Baits,
            bait => $"[#{bait.Id}] {bait.Name}",
            selectedName,
            bait => condition.Params["id"] = (long)bait.Id);

        ImGui.SameLine();
        DrawIntCompareParams(condition, "##baitcount_op", "Count", clamp: v => Math.Max(0, v));
    }
}
