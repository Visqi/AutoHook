using static AutoHook.Conditions.IConditionDefinition;

namespace AutoHook.Conditions.Definitions;

public sealed class SpectralActiveCD : IConditionDefinition {
    public string Id => nameof(SpectralActiveCD);
    public string Name => "Spectral current";
    public string Category => "Fishing";
    public string Description => "Checks whether a spectral current is currently active.";
    public ConditionScopeFlags AllowedScopes => ConditionScopeFlags.Hook | ConditionScopeFlags.FishIgnore | ConditionScopeFlags.AutoCast;

    public bool Evaluate(WorldState world, IReadOnlyDictionary<string, object> parameters) {
        var result = world.SpectralCurrentStatus == SpectralCurrentStatus.Active;
        return GetBool(parameters, "inv", false) ? !result : result;
    }

    public void DrawParams(Condition condition) { }
}
