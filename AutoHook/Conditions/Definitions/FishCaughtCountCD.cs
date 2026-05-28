using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using static AutoHook.Conditions.IConditionDefinition;

namespace AutoHook.Conditions.Definitions;

public sealed class FishCaughtCountCD : IConditionDefinition, ISimpleConditionValue<(bool Enabled, int Limit)> {
    public string Id => nameof(FishCaughtCountCD);
    public string Name => "Fish caught count";
    public string Category => "Counters";
    public string Description => "Compares how many times a fish has been caught this session against a threshold.";
    public ConditionScopeFlags AllowedScopes => ConditionScopeFlags.FishIgnore | ConditionScopeFlags.Hook;

    public readonly record struct FishCaughtParams(int FishId, int Value, string Op, bool Invert) {
        public Dictionary<string, object> ToParams() {
            var dict = new Dictionary<string, object> {
                ["id"] = (long)FishId,
                ["val"] = (long)Value,
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
        if (args.FishId <= 0)
            return args.Invert; // no valid id configured: treat as always-false unless inverted

        var count = world.GetFishCaughtCount(args.FishId);
        var result = CompareInt(count, args.Value, args.Op);
        return args.Invert ? !result : result;
    }

    public void DrawParams(Condition condition) {
        var args = GetParams(condition.Params);
        var fishId = args.FishId;
        var value = args.Value;
        var op = args.Op;

        ImGui.SetNextItemWidth(70 * ImGuiHelpers.GlobalScale);
        if (ImGui.InputInt("Fish ID", ref fishId)) {
            fishId = Math.Max(0, fishId);
            args = args with { FishId = fishId };
            condition.Params = args.ToParams();
        }

        ImGui.SameLine();
        var label = op is ">" or "<" or "<=" or "=" ? op : ">=";
        ImGui.SetNextItemWidth(50 * ImGuiHelpers.GlobalScale);
        using var combo = ImRaii.Combo("##fish_count_op", label);
        if (combo) {
            foreach (var choice in new[] { ">", ">=", "<", "<=", "=" }) {
                var sel = choice == op;
                if (!ImGui.Selectable(choice, sel))
                    continue;

                args = args with { Op = choice };
                condition.Params = args.ToParams();
            }
        }

        ImGui.SameLine();
        ImGui.SetNextItemWidth(60 * ImGuiHelpers.GlobalScale);
        if (ImGui.InputInt("Count", ref value)) {
            value = Math.Max(1, value);
            args = args with { Value = value };
            condition.Params = args.ToParams();
        }
    }

    private static FishCaughtParams GetParams(IReadOnlyDictionary<string, object> p) {
        var fishId = GetInt(p, "id", 0);
        var value = GetInt(p, "val", 1);
        var op = GetOp(p, "op", ">=");
        var invert = GetBool(p, "inv", false);
        return new FishCaughtParams(fishId, value, op, invert);
    }

    (bool Enabled, int Limit) ISimpleConditionValue<(bool Enabled, int Limit)>.FromParams(IReadOnlyDictionary<string, object> p)
        => (true, Math.Max(1, GetInt(p, "val", 1)));

    IReadOnlyDictionary<string, object>? ISimpleConditionValue<(bool Enabled, int Limit)>.ToParams((bool Enabled, int Limit) value, object? context) {
        if (!value.Enabled) return null;
        var fishId = context is int id ? id : 0;
        return new FishCaughtParams(fishId, value.Limit, ">=", false).ToParams();
    }
}

