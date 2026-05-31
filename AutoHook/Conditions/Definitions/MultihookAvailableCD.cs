using FFXIVClientStructs.FFXIV.Client.Game;
using static AutoHook.Conditions.IConditionDefinition;

namespace AutoHook.Conditions.Definitions;

public sealed class MultihookAvailableCD : BoolInvertConditionDefinition {
    public override string Id => nameof(MultihookAvailableCD);
    public override string Name => "Multihook";
    public override ConditionScopeFlags AllowedScopes => ConditionScopeFlags.Hook | ConditionScopeFlags.AutoCast;

    protected override bool ReadValue(WorldState world)
        => world.ActionAvailable(IDs.Actions.MultiHook, ActionType.EventAction);
}
