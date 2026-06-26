using static AutoHook.Conditions.IConditionDefinition;

namespace AutoHook.Conditions.Definitions;

public sealed class OceanLastFishPointsCD : IConditionDefinition {
    public string Id => nameof(OceanLastFishPointsCD);
    public string Name => "Last ocean fish points";
    public ConditionScopeFlags AllowedScopes => ConditionScopeFlags.Hook | ConditionScopeFlags.FishIgnore | ConditionScopeFlags.AutoCast;

    public bool Evaluate(WorldState world, IReadOnlyDictionary<string, object> parameters) {
        var points = GetLastOceanFishPointsValue(world);
        if (points == null)
            return GetBool(parameters, "inv", false);

        var args = GetIntCompareParams(parameters);
        var result = CompareInt(points.Value, args.Value, args.Op);
        return args.Apply(result);
    }

    public void DrawParams(Condition condition)
        => DrawIntCompareParams(condition, "##ocean_points_op", "Points", defaultValue: 300, clamp: v => Math.Max(0, v));

    private static int? GetLastOceanFishPointsValue(WorldState w) {
        var of = w.OceanFishing;
        if (of.FishData == null || of.FishData.Count < 60) return null;
        var zone = (int)Math.Clamp(of.CurrentZone, 0, 2);
        var start = zone * 20;
        for (var i = start + 19; i >= start; i--) {
            var f = of.FishData[i];
            if (f.ItemId == 0) continue;
            var count = f.NqAmount + f.HqAmount;
            if (count == 0) return (int)f.TotalPoints;
            return (int)(f.TotalPoints / count);
        }
        return null;
    }

    public string DescribeParameters(IReadOnlyDictionary<string, object> parameters)
        => ConditionParameterFormat.FormatIntCompare(parameters, defaultValue: 300);
}
