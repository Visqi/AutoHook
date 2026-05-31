using static AutoHook.Conditions.IConditionDefinition;

namespace AutoHook.Conditions.Definitions;

public sealed class SpectralTimerCD : IConditionDefinition {
    public string Id => nameof(SpectralTimerCD);
    public string Name => "Spectral time";
    public string Category => "Fishing";
    public string Description => "Compares remaining ocean spectral current time against a value.";
    public ConditionScopeFlags AllowedScopes => ConditionScopeFlags.Hook | ConditionScopeFlags.FishIgnore | ConditionScopeFlags.AutoCast;

    public bool Evaluate(WorldState world, IReadOnlyDictionary<string, object> parameters) {
        var args = GetIntCompareParams(parameters, "sec");
        if (!world.SpectralTimer.IsActive)
            return args.Invert;
        var lhs = (int)Math.Floor(world.SpectralTimeRemaining);
        var result = CompareInt(lhs, args.Value, args.Op);
        return args.Invert ? !result : result;
    }

    public void DrawParams(Condition condition)
        => DrawIntCompareParams(condition, "##spectral_op", "Seconds", valueKey: "sec", clamp: v => Math.Max(0, v));
}
