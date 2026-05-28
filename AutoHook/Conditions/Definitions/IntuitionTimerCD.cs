using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using static AutoHook.Conditions.IConditionDefinition;

namespace AutoHook.Conditions.Definitions;

public sealed class IntuitionTimerCD : IConditionDefinition {
    public string Id => nameof(IntuitionTimerCD);
    public string Name => "Intuition time";
    public string Category => "Fishing";
    public string Description => "Compares remaining Fisher's Intuition time against a value.";
    public ConditionScopeFlags AllowedScopes => ConditionScopeFlags.Hook | ConditionScopeFlags.FishIgnore | ConditionScopeFlags.AutoCast;

    public readonly record struct IntuitionTimeParams(int Seconds, string Op, bool Invert) {
        public Dictionary<string, object> ToParams() {
            var dict = new Dictionary<string, object>();
            if (Seconds != 0)
                dict["sec"] = (long)Seconds;
            if (!string.IsNullOrEmpty(Op) && Op != ">=")
                dict["op"] = Op;
            if (Invert)
                dict["inv"] = true;
            return dict;
        }
    }

    public bool Evaluate(WorldState world, IReadOnlyDictionary<string, object> parameters) {
        var args = GetParams(parameters);
        if (world.Fishing.Intuition.Status != IntuitionStatus.Active) return args.Invert;
        var lhs = (int)Math.Floor(world.Fishing.Intuition.TimeRemaining);
        var rhs = args.Seconds;
        var result = CompareInt(lhs, rhs, args.Op);
        return args.Invert ? !result : result;
    }

    public void DrawParams(Condition condition) {
        var args = GetParams(condition.Params);
        var sec = args.Seconds;
        var label = args.Op is ">" or "<" or "<=" or "=" ? args.Op : ">=";
        ImGui.SetNextItemWidth(50 * ImGuiHelpers.GlobalScale);
        using var combo = ImRaii.Combo("##intu_op", label);
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
        if (ImGui.InputInt("Seconds", ref sec)) {
            sec = Math.Max(0, sec);
            args = args with { Seconds = sec };
            condition.Params = args.ToParams();
        }
    }

    private static IntuitionTimeParams GetParams(IReadOnlyDictionary<string, object> p) {
        var sec = GetDouble(p, "sec", 0);
        var op = GetOp(p, "op", ">=");
        var inv = GetBool(p, "inv", false);
        var secondsInt = (int)Math.Floor(sec);
        return new IntuitionTimeParams(secondsInt, op, inv);
    }
}
