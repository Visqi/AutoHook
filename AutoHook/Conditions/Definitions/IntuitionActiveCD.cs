using static AutoHook.Conditions.IConditionDefinition;

namespace AutoHook.Conditions.Definitions;

public sealed class IntuitionActiveCD : IConditionDefinition {
    public string Id => nameof(IntuitionActiveCD);
    public string Name => "Fisher's Intuition";
    public string Category => "Fishing";
    public string Description => "Checks whether Fisher's Intuition is currently active.";
    public ConditionScopeFlags AllowedScopes => ConditionScopeFlags.All;

    public bool Evaluate(WorldState world, IReadOnlyDictionary<string, object> parameters) {
        var result = world.IntuitionStatus == IntuitionStatus.Active;
        return GetBool(parameters, "inv", false) ? !result : result;
    }

    public void DrawParams(Condition condition) { }
}
