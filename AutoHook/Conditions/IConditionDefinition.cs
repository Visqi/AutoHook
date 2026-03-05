using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using System.Reflection;

namespace AutoHook.Conditions;

/// <summary>
/// Pluggable definition for a single condition type.
/// Implement this once per condition to define its ID, metadata,
/// evaluation logic, and UI drawing in a single place.
/// </summary>
public interface IConditionDefinition
{
    string Id { get; }
    string Name { get; }
    string Category { get; }
    string Description { get; }
    ConditionScopeFlags AllowedScopes { get; }

    bool Evaluate(WorldState world, IReadOnlyDictionary<string, object> parameters);

    void DrawParams(Condition condition);

    public static List<uint> GetIds(IReadOnlyDictionary<string, object> p)
    {
        if (!p.TryGetValue("ids", out var o)) return [];
        if (o is List<object> list)
            return [.. list.Select(x => Convert.ToUInt32(x))];
        if (o is long single) return [(uint)single];
        return [];
    }

    public static List<uint> GetStatusIds(IReadOnlyDictionary<string, object> p)
    {
        if (!p.TryGetValue("ids", out var o)) return [];
        if (o is List<object> list)
            return [.. list.Select(x => Convert.ToUInt32(x))];
        if (o is long single) return [(uint)single];
        return [];
    }

    public static List<byte> GetWeatherIds(IReadOnlyDictionary<string, object> p)
    {
        if (!p.TryGetValue("ids", out var o)) return [];
        if (o is List<object> list)
            return [.. list.Select(x => Convert.ToByte(x))];
        if (o is long single) return [(byte)single];
        return [];
    }

    public static bool GetBool(IReadOnlyDictionary<string, object> p, string key, bool def)
    {
        if (!p.TryGetValue(key, out var o)) return def;
        if (o is bool b) return b;
        if (o is long l) return l != 0;
        return def;
    }

    public static int GetInt(IReadOnlyDictionary<string, object> p, string key, int def)
        => !p.TryGetValue(key, out var o) ? def : Convert.ToInt32(o);

    public static uint GetUInt(IReadOnlyDictionary<string, object> p, string key, uint def)
        => !p.TryGetValue(key, out var o) ? def : Convert.ToUInt32(o);

    public static double GetDouble(IReadOnlyDictionary<string, object> p, string key, double def)
        => !p.TryGetValue(key, out var o) ? def : Convert.ToDouble(o);

    public static string GetOp(IReadOnlyDictionary<string, object> p, string key, string def)
        => !p.TryGetValue(key, out var o) || o == null ? def : o.ToString() ?? def;

    public static bool CompareInt(int lhs, int rhs, string op)
    {
        return op switch
        {
            ">" => lhs > rhs,
            ">=" => lhs >= rhs,
            "<" => lhs < rhs,
            "<=" => lhs <= rhs,
            "=" => lhs == rhs,
            _ => lhs >= rhs,
        };
    }

    public static void DrawIdsParams(Condition cond, string label)
    {
        var ids = GetIds(cond.Params);
        var text = string.Join(", ", ids);
        var buf = text;
        ImGui.SetNextItemWidth(140 * ImGuiHelpers.GlobalScale);
        if (!ImGui.InputText(label, ref buf, 128))
            return;

        var list = new List<object>();
        foreach (var part in buf.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            if (uint.TryParse(part.Trim(), out var id))
                list.Add((long)id);
        }

        if (list.Count > 0)
            cond.Params["ids"] = list;
        else
            cond.Params.Remove("ids");
    }

    public static List<(double min, double max)> GetRanges(IReadOnlyDictionary<string, object> p)
    {
        if (!p.TryGetValue("r", out var o) || o is not List<object> list) return [];
        var result = new List<(double, double)>();
        for (var i = 0; i + 1 < list.Count; i += 2)
            result.Add((Convert.ToDouble(list[i]), Convert.ToDouble(list[i + 1])));
        return result;
    }

    public readonly record struct RangeParams(IReadOnlyList<(double Min, double Max)> Ranges, bool Invert)
    {
        public Dictionary<string, object> ToParams()
        {
            var dict = new Dictionary<string, object>();
            if (Ranges.Count > 0)
            {
                var list = new List<object>(Ranges.Count * 2);
                foreach (var (min, max) in Ranges)
                {
                    list.Add(min);
                    list.Add(max);
                }
                dict["r"] = list;
            }
            if (Invert)
                dict["inv"] = true;
            return dict;
        }
    }

    public static RangeParams GetRangeParams(IReadOnlyDictionary<string, object> p)
    {
        var ranges = GetRanges(p);
        var inv = GetBool(p, "inv", false);
        return new RangeParams(ranges, inv);
    }
}

public static class ConditionDefinitionExtensions
{
    public static ConditionTypeDef ToTypeDef(this IConditionDefinition def)
        => new()
        {
            Id = def.Id,
            Name = def.Name,
            Category = def.Category,
            Description = def.Description,
            AllowedScopes = def.AllowedScopes,
            Evaluate = def.Evaluate,
            DrawParams = def.DrawParams,
        };
}

