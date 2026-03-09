using FFXIVClientStructs.FFXIV.Client.Game;
using static AutoHook.Conditions.IConditionDefinition;

namespace AutoHook.Conditions.Definitions;

public sealed class MultihookAvailableCD : IConditionDefinition {
    public string Id => nameof(MultihookAvailableCD);
    public string Name => "Multihook";
    public string Category => "Fishing";
    public string Description => "Checks whether the Multihook duty action has at least one charge.";
    public ConditionScopeFlags AllowedScopes => ConditionScopeFlags.Hook | ConditionScopeFlags.AutoCast;

    public bool Evaluate(WorldState world, IReadOnlyDictionary<string, object> parameters) {
        var result = world.ActionAvailable(IDs.Actions.MultiHook, ActionType.EventAction);
        return GetBool(parameters, "inv", false) ? !result : result;
    }

    public void DrawParams(Condition condition) { }
}
