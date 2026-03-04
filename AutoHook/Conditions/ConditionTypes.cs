using FFXIVClientStructs.FFXIV.Client.Game;
using Lumina.Excel.Sheets;

namespace AutoHook.Conditions;

/// <summary>
/// Built-in condition type definitions. Params use minimal keys; only non-default are serialized.
/// </summary>
public static class ConditionTypes
{
    public static void RegisterAll(ConditionRegistry registry)
    {
        // ---- Status ----
        // Params: "ids" = list of status IDs (any), optional "inv" = true for "condition not true"
        registry.Register(new ConditionTypeDef
        {
            Id = ConditionId.StatusActive,
            Name = "Status",
            Category = "Status",
            Evaluate = (w, p) =>
            {
                var ids = GetStatusIds(p);
                if (ids.Count == 0) return GetBool(p, "inv", false);
                var result = ids.Any(w.HasStatus);
                return GetBool(p, "inv", false) ? !result : result;
            },
        });

        // Params: "ids" = list, "minStacks" = optional int, "op" = >,>=,<,<=,=, "inv" = optional
        registry.Register(new ConditionTypeDef
        {
            Id = ConditionId.StatusStacks,
            Name = "Status stacks",
            Category = "Status",
            Evaluate = (w, p) =>
            {
                var ids = GetStatusIds(p);
                var minStacks = GetInt(p, "minStacks", 1);
                if (ids.Count == 0) return false;
                var op = GetOp(p, "op", ">=");
                var result = ids.Any(id => CompareInt(w.GetStatusStacks(id), minStacks, op));
                return GetBool(p, "inv", false) ? !result : result;
            },
        });

        // ---- GP ----
        // Params: "val" = int, "op" = one of >, >=, <, <=, =, "inv" = optional
        registry.Register(new ConditionTypeDef
        {
            Id = ConditionId.Gp,
            Name = "GP",
            Category = "Player",
            Evaluate = (w, p) =>
            {
                var val = GetInt(p, "val", 0);
                var op = GetOp(p, "op", ">=");
                var result = CompareInt((int)w.CurrentGp, val, op);
                return GetBool(p, "inv", false) ? !result : result;
            },
        });

        // ---- Bite timer ----
        // Params: "r" = ranges, "inv" = optional
        registry.Register(new ConditionTypeDef
        {
            Id = ConditionId.BiteTimer,
            Name = "Bite timer",
            Category = "Time",
            Evaluate = (w, p) =>
            {
                var ranges = GetRanges(p);
                if (ranges.Count == 0) return true;
                var t = w.BiteTimeSeconds;
                var result = false;
                foreach (var (min, max) in ranges)
                {
                    if (t >= min && (max <= 0 || t <= max)) { result = true; break; }
                }
                return GetBool(p, "inv", false) ? !result : result;
            },
        });

        // ---- Chum timer ----
        registry.Register(new ConditionTypeDef
        {
            Id = ConditionId.ChumTimer,
            Name = "Chum timer",
            Category = "Time",
            Evaluate = (w, p) =>
            {
                if (!w.ChumActive) return GetBool(p, "inv", false);
                var ranges = GetRanges(p);
                if (ranges.Count == 0) return true;
                var t = w.BiteTimeSeconds;
                var result = false;
                foreach (var (min, max) in ranges)
                {
                    if (t >= min && (max <= 0 || t <= max)) { result = true; break; }
                }
                return GetBool(p, "inv", false) ? !result : result;
            },
        });

        // ---- Intuition ----
        registry.Register(new ConditionTypeDef
        {
            Id = ConditionId.IntuitionActive,
            Name = "Fisher's Intuition",
            Category = "Fishing",
            Evaluate = (w, p) =>
            {
                var result = w.IntuitionStatus == IntuitionStatus.Active;
                return GetBool(p, "inv", false) ? !result : result;
            },
        });

        // Params: "sec" = seconds remaining, "op" = >,>=,<,<=,=, "inv" = optional
        registry.Register(new ConditionTypeDef
        {
            Id = ConditionId.IntuitionTime,
            Name = "Intuition time",
            Category = "Fishing",
            Evaluate = (w, p) =>
            {
                if (w.IntuitionStatus != IntuitionStatus.Active) return GetBool(p, "inv", false);
                var sec = GetDouble(p, "sec", 0);
                var op = GetOp(p, "op", ">=");
                var lhs = (int)Math.Floor(w.IntuitionTimeRemaining);
                var rhs = (int)Math.Floor(sec);
                var result = CompareInt(lhs, rhs, op);
                return GetBool(p, "inv", false) ? !result : result;
            },
        });

        // ---- Spectral ----
        registry.Register(new ConditionTypeDef
        {
            Id = ConditionId.SpectralActive,
            Name = "Spectral current",
            Category = "Fishing",
            Evaluate = (w, p) =>
            {
                var result = w.SpectralCurrentStatus == SpectralCurrentStatus.Active;
                return GetBool(p, "inv", false) ? !result : result;
            },
        });

        // ---- Weather ----
        // Params: "ids" = list of weather IDs (by name), "inv" = optional
        registry.Register(new ConditionTypeDef
        {
            Id = ConditionId.Weather,
            Name = "Weather",
            Category = "World",
            Evaluate = (w, p) =>
            {
                var ids = GetWeatherIds(p);
                if (ids.Count == 0) return false;

                var sheet = Svc.Data.GetExcelSheet<Weather>();
                if (sheet == null)
                {
                    var result = ids.Contains(w.CurrentWeatherId);
                    return GetBool(p, "inv", false) ? !result : result;
                }

                if (!sheet.TryGetRow(w.CurrentWeatherId, out var current))
                    return GetBool(p, "inv", false);
                var currentName = current.Name.ToString();
                if (string.IsNullOrEmpty(currentName))
                    return GetBool(p, "inv", false);

                var match = false;
                foreach (var id in ids)
                {
                    if (sheet.TryGetRow(id, out var row) && row.Name.ToString() == currentName)
                    {
                        match = true;
                        break;
                    }
                }
                return GetBool(p, "inv", false) ? !match : match;
            },
        });

        // ---- Action available ----
        // Params: "id", "type", "inv" = optional
        registry.Register(new ConditionTypeDef
        {
            Id = ConditionId.ActionAvailable,
            Name = "Action",
            Category = "Player",
            Evaluate = (w, p) =>
            {
                var id = GetUInt(p, "id", 0);
                if (id == 0) return false;
                var type = (ActionType)GetInt(p, "type", 0);
                var result = w.ActionAvailable(id, type);
                return GetBool(p, "inv", false) ? !result : result;
            },
        });

        // ---- Multihook (duty action) ----
        registry.Register(new ConditionTypeDef
        {
            Id = ConditionId.MultihookAvailable,
            Name = "Multihook",
            Category = "Fishing",
            Evaluate = (w, p) =>
            {
                var result = w.ActionAvailable(IDs.Actions.MultiHook, ActionType.EventAction);
                return GetBool(p, "inv", false) ? !result : result;
            },
        });

        // ---- Ocean fishing mission type ----
        // Params: "ids" = list of IKDPlayerMissionCondition row IDs (any of Mission1/2/3Type matches), "inv" = optional
        registry.Register(new ConditionTypeDef
        {
            Id = ConditionId.OceanMissionType,
            Name = "Ocean mission type",
            Category = "Fishing",
            Evaluate = (w, p) =>
            {
                var ids = GetMissionTypeIds(p);
                if (ids.Count == 0) return false;
                var of = w.OceanFishing;
                var result = ids.Contains(of.Mission1Type) || ids.Contains(of.Mission2Type) || ids.Contains(of.Mission3Type);
                return GetBool(p, "inv", false) ? !result : result;
            },
        });

        // ---- Ocean fishing mission progress ----
        // Params: "mission" = 1/2/3, "val" = required progress (ushort), "op" = >,>=,<,<=,=, "inv" = optional
        registry.Register(new ConditionTypeDef
        {
            Id = ConditionId.OceanMissionProgress,
            Name = "Ocean mission progress",
            Category = "Fishing",
            Evaluate = (w, p) =>
            {
                var slot = GetInt(p, "mission", 1);
                var val = GetInt(p, "val", 0);
                var op = GetOp(p, "op", ">=");
                var of = w.OceanFishing;
                var progress = slot switch
                {
                    2 => of.Mission2Progress,
                    3 => of.Mission3Progress,
                    _ => of.Mission1Progress,
                };
                var result = CompareInt(progress, val, op);
                return GetBool(p, "inv", false) ? !result : result;
            },
        });

        // ---- Swimbait count ----
        // Params: "val" = count, "op" = >,>=,<,<=,=, "inv" = optional
        registry.Register(new ConditionTypeDef
        {
            Id = ConditionId.SwimbaitCount,
            Name = "Swimbait count",
            Category = "Fishing",
            Evaluate = (w, p) =>
            {
                var val = GetInt(p, "val", 0);
                var op = GetOp(p, "op", ">=");
                var count = w.GetSwimbaitCount();
                var result = CompareInt(count, val, op);
                return GetBool(p, "inv", false) ? !result : result;
            },
        });
    }

    private static List<uint> GetMissionTypeIds(IReadOnlyDictionary<string, object> p)
    {
        if (!p.TryGetValue("ids", out var o)) return [];
        if (o is List<object> list)
            return [.. list.Select(x => Convert.ToUInt32(x))];
        if (o is long single) return [(uint)single];
        return [];
    }

    private static List<uint> GetStatusIds(IReadOnlyDictionary<string, object> p)
    {
        if (!p.TryGetValue("ids", out var o)) return [];
        if (o is List<object> list)
            return [.. list.Select(x => Convert.ToUInt32(x))];
        if (o is long single) return [(uint)single];
        return [];
    }

    private static List<byte> GetWeatherIds(IReadOnlyDictionary<string, object> p)
    {
        if (!p.TryGetValue("ids", out var o)) return [];
        if (o is List<object> list)
            return [.. list.Select(x => Convert.ToByte(x))];
        if (o is long single) return [(byte)single];
        return [];
    }

    private static bool GetBool(IReadOnlyDictionary<string, object> p, string key, bool def)
    {
        if (!p.TryGetValue(key, out var o)) return def;
        if (o is bool b) return b;
        if (o is long l) return l != 0;
        return def;
    }

    private static int GetInt(IReadOnlyDictionary<string, object> p, string key, int def)
        => !p.TryGetValue(key, out var o) ? def : Convert.ToInt32(o);

    private static uint GetUInt(IReadOnlyDictionary<string, object> p, string key, uint def)
        => !p.TryGetValue(key, out var o) ? def : Convert.ToUInt32(o);

    private static double GetDouble(IReadOnlyDictionary<string, object> p, string key, double def)
        => !p.TryGetValue(key, out var o) ? def : Convert.ToDouble(o);

    /// <summary>ranges: list of [min, max]; max 0 = no upper bound.</summary>
    private static List<(double min, double max)> GetRanges(IReadOnlyDictionary<string, object> p)
    {
        if (!p.TryGetValue("r", out var o) || o is not List<object> list) return [];
        var result = new List<(double, double)>();
        for (var i = 0; i + 1 < list.Count; i += 2)
            result.Add((Convert.ToDouble(list[i]), Convert.ToDouble(list[i + 1])));
        return result;
    }

    private static string GetOp(IReadOnlyDictionary<string, object> p, string key, string def)
        => !p.TryGetValue(key, out var o) || o == null ? def : o.ToString() ?? def;

    private static bool CompareInt(int lhs, int rhs, string op)
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
}
