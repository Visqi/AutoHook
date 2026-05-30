using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using static AutoHook.Conditions.IConditionDefinition;

namespace AutoHook.Conditions.Definitions;

public sealed class MaxGpCD : IConditionDefinition {
    public string Id => nameof(MaxGpCD);
    public string Name => "Max GP";
    public string Category => "Player";
    public string Description => "Compares maximum GP against a value.";
    public ConditionScopeFlags AllowedScopes => ConditionScopeFlags.Hook | ConditionScopeFlags.AutoCordial | ConditionScopeFlags.AutoCast;

    public readonly record struct MaxGpParams(int Value, string Op, bool Invert) {
        public Dictionary<string, object> ToParams() {
            var dict = new Dictionary<string, object> {
                ["val"] = (long)Value
            };
            if (!string.IsNullOrEmpty(Op) && Op != ">=")
                dict["op"] = Op;
            if (Invert)
                dict["inv"] = true;
            return dict;
        }
    }

    public bool Evaluate(WorldState world, IReadOnlyDictionary<string, object> parameters) {
        var args = GetParams(parameters);
        var result = CompareInt((int)world.MaxGp, args.Value, args.Op);
        return args.Invert ? !result : result;
    }

    public void DrawParams(Condition condition) {
        var args = GetParams(condition.Params);
        var val = args.Value;
        var label = args.Op is ">" or "<" or "<=" or "=" ? args.Op : ">=";
        ImGui.SetNextItemWidth(50 * ImGuiHelpers.GlobalScale);
        using var combo = ImRaii.Combo("##maxgp_op", label);
        if (combo) {
            foreach (var choice in new[] { ">", ">=", "<", "<=", "=" }) {
                var sel = choice == args.Op;
                if (!ImGui.Selectable(choice, sel))
                    continue;

                args = args with { Op = choice };
                condition.Params = args.ToParams();
            }
        }

        ImGui.SameLine();
        ImGui.SetNextItemWidth(80 * ImGuiHelpers.GlobalScale);
        if (ImGui.InputInt("Max GP", ref val)) {
            args = args with { Value = val };
            condition.Params = args.ToParams();
        }
    }

    private static MaxGpParams GetParams(IReadOnlyDictionary<string, object> p) {
        var value = GetInt(p, "val", 0);
        var op = GetOp(p, "op", ">=");
        var inv = GetBool(p, "inv", false);
        return new MaxGpParams(value, op, inv);
    }
}
