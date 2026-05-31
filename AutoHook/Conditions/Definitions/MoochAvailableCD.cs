using static AutoHook.Conditions.IConditionDefinition;

namespace AutoHook.Conditions.Definitions;

public sealed class MoochAvailableCD : BoolInvertConditionDefinition, ISimpleConditionValue<bool> {
    public override string Id => nameof(MoochAvailableCD);
    public override string Name => "Mooch available";
    public override ConditionScopeFlags AllowedScopes => ConditionScopeFlags.Hook | ConditionScopeFlags.AutoCast;

    public readonly record struct MoochAvailableParams(bool Invert) {
        public Dictionary<string, object> ToParams() {
            var dict = new Dictionary<string, object>();
            if (Invert)
                dict["inv"] = true;
            return dict;
        }
    }

    protected override bool ReadValue(WorldState world)
        => world.IsMoochAvailable();

    bool ISimpleConditionValue<bool>.FromParams(IReadOnlyDictionary<string, object> p)
        => GetBool(p, "inv", false);

    IReadOnlyDictionary<string, object>? ISimpleConditionValue<bool>.ToParams(bool value, object? context)
        => value ? new MoochAvailableParams(true).ToParams() : null;
}
