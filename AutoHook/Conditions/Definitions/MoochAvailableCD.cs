using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace AutoHook.Conditions.Definitions;

public sealed class MoochAvailableCD : IConditionDefinition, ISimpleConditionValue<bool>
{
    public string Id => nameof(MoochAvailableCD);
    public string Name => "Mooch available";
    public string Category => "Actions";
    public string Description => "Checks whether Mooch or Mooch II is currently available.";
    public ConditionScopeFlags AllowedScopes => ConditionScopeFlags.Hook | ConditionScopeFlags.AutoCast;

    public readonly record struct MoochAvailableParams(bool Invert)
    {
        public Dictionary<string, object> ToParams()
        {
            var dict = new Dictionary<string, object>();
            if (Invert)
                dict["inv"] = true;
            return dict;
        }
    }

    public bool Evaluate(WorldState world, IReadOnlyDictionary<string, object> parameters)
    {
        var invert = IConditionDefinition.GetBool(parameters, "inv", false);
        var available = world.IsMoochAvailable();
        return invert ? !available : available;
    }

    public void DrawParams(Condition condition)
    {
        var invert = IConditionDefinition.GetBool(condition.Params, "inv", false);
        ImGui.SetNextItemWidth(180 * ImGuiHelpers.GlobalScale);
        if (ImGui.Checkbox(UIStrings.OnlyWhenMoochNotAvailable, ref invert))
        {
            var args = new MoochAvailableParams(invert);
            condition.Params = args.ToParams();
        }
    }

    bool ISimpleConditionValue<bool>.FromParams(IReadOnlyDictionary<string, object> p)
        => IConditionDefinition.GetBool(p, "inv", false);

    IReadOnlyDictionary<string, object>? ISimpleConditionValue<bool>.ToParams(bool value, object? context)
        => value ? new MoochAvailableParams(true).ToParams() : null;
}

