using static AutoHook.Conditions.IConditionDefinition;

namespace AutoHook.Conditions.Definitions;

public abstract class BoolInvertConditionDefinition : IConditionDefinition {
    public abstract string Id { get; }
    public abstract string Name { get; }
    public abstract ConditionScopeFlags AllowedScopes { get; }

    protected abstract bool ReadValue(WorldState world);

    public bool Evaluate(WorldState world, IReadOnlyDictionary<string, object> parameters) {
        var result = ReadValue(world);
        return GetBool(parameters, "inv", false) ? !result : result;
    }

    public void DrawParams(Condition condition) { }
}
