using static AutoHook.Conditions.IConditionDefinition;

namespace AutoHook.Conditions.Definitions;

public abstract class IntCompareConditionDefinition : IConditionDefinition {
    public abstract string Id { get; }
    public abstract string Name { get; }
    public abstract ConditionScopeFlags AllowedScopes { get; }

    protected virtual string ValueKey => "val";
    protected virtual int DefaultValue => 0;
    protected virtual string DefaultOp => ">=";
    protected virtual string ComboId => $"##{Id}_op";
    protected virtual string ValueLabel => "Value";
    protected virtual float ValueWidth => 80f;
    protected virtual Func<int, int>? Clamp => null;

    protected abstract int ReadValue(WorldState world, IReadOnlyDictionary<string, object> parameters);

    protected virtual bool? InactiveResult(WorldState world, IReadOnlyDictionary<string, object> parameters) => null;

    public bool Evaluate(WorldState world, IReadOnlyDictionary<string, object> parameters) {
        if (InactiveResult(world, parameters) is bool inactive)
            return inactive;

        var args = GetIntCompareParams(parameters, valueKey: ValueKey, defaultValue: DefaultValue, defaultOp: DefaultOp);
        return args.Apply(CompareInt(ReadValue(world, parameters), args.Value, args.Op));
    }

    public virtual void DrawParams(Condition condition)
        => DrawIntCompareParams(condition, ComboId, ValueLabel, valueKey: ValueKey, defaultValue: DefaultValue, defaultOp: DefaultOp, clamp: Clamp, valueWidth: ValueWidth);
}
