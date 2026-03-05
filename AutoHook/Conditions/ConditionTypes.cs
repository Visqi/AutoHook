using FFXIVClientStructs.FFXIV.Client.Game;
using Lumina.Excel.Sheets;

namespace AutoHook.Conditions;

public static class ConditionTypes
{
    public readonly record struct StatusActiveParams(IReadOnlyList<uint> Ids, bool Invert);
    public readonly record struct GpParams(int Value, string Op, bool Invert);
    public readonly record struct RangeParams(IReadOnlyList<(double Min, double Max)> Ranges, bool Invert);
    public readonly record struct IntuitionTimeParams(int Seconds, string Op, bool Invert);

    public static StatusActiveParams GetStatusActiveParams(IReadOnlyDictionary<string, object> p)
    {
        var ids = GetStatusIds(p);
        var inv = GetBool(p, "inv", false);
        return new StatusActiveParams(ids, inv);
    }

    public static Dictionary<string, object> ToParams(StatusActiveParams args)
    {
        var dict = new Dictionary<string, object>();
        if (args.Ids.Count > 0)
            dict["ids"] = args.Ids.Select(id => (object)(long)id).ToList();
        if (args.Invert)
            dict["inv"] = true;
        return dict;
    }

    public static GpParams GetGpParams(IReadOnlyDictionary<string, object> p)
    {
        var value = GetInt(p, "val", 0);
        var op = GetOp(p, "op", ">=");
        var inv = GetBool(p, "inv", false);
        return new GpParams(value, op, inv);
    }

    public static Dictionary<string, object> ToParams(GpParams args)
    {
        var dict = new Dictionary<string, object>
        {
            ["val"] = (long)args.Value
        };
        if (!string.IsNullOrEmpty(args.Op) && args.Op != ">=")
            dict["op"] = args.Op;
        if (args.Invert)
            dict["inv"] = true;
        return dict;
    }

    public static RangeParams GetRangeParams(IReadOnlyDictionary<string, object> p)
    {
        var ranges = GetRanges(p);
        var inv = GetBool(p, "inv", false);
        return new RangeParams(ranges, inv);
    }

    public static Dictionary<string, object> ToParams(RangeParams args)
    {
        var dict = new Dictionary<string, object>();
        if (args.Ranges.Count > 0)
        {
            var list = new List<object>(args.Ranges.Count * 2);
            foreach (var (min, max) in args.Ranges)
            {
                list.Add(min);
                list.Add(max);
            }
            dict["r"] = list;
        }
        if (args.Invert)
            dict["inv"] = true;
        return dict;
    }

    public static IntuitionTimeParams GetIntuitionTimeParams(IReadOnlyDictionary<string, object> p)
    {
        var sec = GetDouble(p, "sec", 0);
        var op = GetOp(p, "op", ">=");
        var inv = GetBool(p, "inv", false);
        var secondsInt = (int)Math.Floor(sec);
        return new IntuitionTimeParams(secondsInt, op, inv);
    }

    public static Dictionary<string, object> ToParams(IntuitionTimeParams args)
    {
        var dict = new Dictionary<string, object>();
        if (args.Seconds != 0)
            dict["sec"] = (long)args.Seconds;
        if (!string.IsNullOrEmpty(args.Op) && args.Op != ">=")
            dict["op"] = args.Op;
        if (args.Invert)
            dict["inv"] = true;
        return dict;
    }

    public static void RegisterAll(ConditionRegistry registry)
    {
        // ---- Status ----
        // Params: "ids" = list of status IDs (any), optional "inv" = true for "condition not true"
        registry.Register(new ConditionTypeDef
        {
            Id = ConditionId.StatusActive,
            Name = "Status",
            Category = "Status",
            Description = "Checks whether any of the selected statuses are currently active.",
            AllowedScopes = ConditionScopeFlags.All,
            Evaluate = (w, p) =>
            {
                var args = GetStatusActiveParams(p);
                if (args.Ids.Count == 0) return args.Invert;
                var result = args.Ids.Any(w.HasStatus);
                return args.Invert ? !result : result;
            },
        });

        // Params: "ids" = list, "minStacks" = optional int, "op" = >,>=,<,<=,=, "inv" = optional
        registry.Register(new ConditionTypeDef
        {
            Id = ConditionId.StatusStacks,
            Name = "Status stacks",
            Category = "Status",
            Description = "Checks stacks for selected statuses against a threshold.",
            AllowedScopes = ConditionScopeFlags.Hook | ConditionScopeFlags.FishIgnore | ConditionScopeFlags.AutoCast,
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
            Description = "Compares current GP against a value.",
            AllowedScopes = ConditionScopeFlags.Hook | ConditionScopeFlags.AutoCordial | ConditionScopeFlags.AutoCast,
            Evaluate = (w, p) =>
            {
                var args = GetGpParams(p);
                var result = CompareInt((int)w.CurrentGp, args.Value, args.Op);
                return args.Invert ? !result : result;
            },
        });

        // ---- Bite timer ----
        // Params: "r" = ranges, "inv" = optional
        registry.Register(new ConditionTypeDef
        {
            Id = ConditionId.BiteTimer,
            Name = "Bite timer",
            Category = "Time",
            Description = "Checks current bite timer (seconds since bite) against one or more ranges.",
            AllowedScopes = ConditionScopeFlags.Hook | ConditionScopeFlags.AutoCast,
            Evaluate = (w, p) =>
            {
                var args = GetRangeParams(p);
                var ranges = args.Ranges;
                if (ranges.Count == 0) return true;
                var t = w.BiteTimeSeconds;
                var result = false;
                foreach (var (min, max) in ranges)
                {
                    if (t >= min && (max <= 0 || t <= max)) { result = true; break; }
                }
                return args.Invert ? !result : result;
            },
        });

        // ---- Chum timer ----
        registry.Register(new ConditionTypeDef
        {
            Id = ConditionId.ChumTimer,
            Name = "Chum timer",
            Category = "Time",
            Description = "Checks bite timer while Chum is active against one or more ranges.",
            AllowedScopes = ConditionScopeFlags.Hook | ConditionScopeFlags.FishIgnore | ConditionScopeFlags.AutoCast,
            Evaluate = (w, p) =>
            {
                var args = GetRangeParams(p);
                if (!w.ChumActive) return args.Invert;
                var ranges = args.Ranges;
                if (ranges.Count == 0) return true;
                var t = w.BiteTimeSeconds;
                var result = false;
                foreach (var (min, max) in ranges)
                {
                    if (t >= min && (max <= 0 || t <= max)) { result = true; break; }
                }
                return args.Invert ? !result : result;
            },
        });

        // ---- Intuition ----
        registry.Register(new ConditionTypeDef
        {
            Id = ConditionId.IntuitionActive,
            Name = "Fisher's Intuition",
            Category = "Fishing",
            Description = "Checks whether Fisher's Intuition is currently active.",
            AllowedScopes = ConditionScopeFlags.All,
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
            Description = "Compares remaining Fisher's Intuition time against a value.",
            AllowedScopes = ConditionScopeFlags.Hook | ConditionScopeFlags.FishIgnore | ConditionScopeFlags.AutoCast,
            Evaluate = (w, p) =>
            {
                var args = GetIntuitionTimeParams(p);
                if (w.IntuitionStatus != IntuitionStatus.Active) return args.Invert;
                var lhs = (int)Math.Floor(w.IntuitionTimeRemaining);
                var rhs = args.Seconds;
                var result = CompareInt(lhs, rhs, args.Op);
                return args.Invert ? !result : result;
            },
        });

        // ---- Spectral ----
        registry.Register(new ConditionTypeDef
        {
            Id = ConditionId.SpectralActive,
            Name = "Spectral current",
            Category = "Fishing",
            Description = "Checks whether a spectral current is currently active.",
            AllowedScopes = ConditionScopeFlags.Hook | ConditionScopeFlags.FishIgnore | ConditionScopeFlags.AutoCast,
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
            Description = "Checks current weather (by name) against one or more weather entries.",
            AllowedScopes = ConditionScopeFlags.Hook | ConditionScopeFlags.FishIgnore | ConditionScopeFlags.AutoCast,
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
            Description = "Checks whether an action/item/event action is currently usable.",
            AllowedScopes = ConditionScopeFlags.Hook | ConditionScopeFlags.AutoCast,
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
            Description = "Checks whether the Multihook duty action has at least one charge.",
            AllowedScopes = ConditionScopeFlags.Hook | ConditionScopeFlags.AutoCast,
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
            Description = "Matches current ocean fishing mission types (slots 1–3) against selected entries.",
            AllowedScopes = ConditionScopeFlags.Hook | ConditionScopeFlags.FishIgnore | ConditionScopeFlags.AutoCast,
            Evaluate = (w, p) =>
            {
                var ids = GetIds(p);
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
            Description = "Compares progress of a selected ocean fishing mission (1–3) against a value.",
            AllowedScopes = ConditionScopeFlags.Hook | ConditionScopeFlags.FishIgnore | ConditionScopeFlags.AutoCast,
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

        // ---- Ocean fishing route ----
        // Params: "ids" = list of route row IDs (matches CurrentRoute), "inv" = optional
        registry.Register(new ConditionTypeDef
        {
            Id = ConditionId.OceanRoute,
            Name = "Ocean route",
            Category = "Fishing",
            Description = "Matches the current ocean fishing route against selected routes.",
            AllowedScopes = ConditionScopeFlags.Hook | ConditionScopeFlags.FishIgnore | ConditionScopeFlags.AutoCast,
            Evaluate = (w, p) =>
            {
                var ids = GetIds(p);
                if (ids.Count == 0) return false;
                var route = w.OceanFishing.CurrentRoute;
                var result = ids.Contains(route);
                return GetBool(p, "inv", false) ? !result : result;
            },
        });

        // ---- Ocean fishing zone ----
        // Params: "zone" = 0/1/2 (CurrentZone index), "inv" = optional
        registry.Register(new ConditionTypeDef
        {
            Id = ConditionId.OceanZone,
            Name = "Ocean zone",
            Category = "Fishing",
            Description = "Matches the current ocean fishing zone index (1–3).",
            AllowedScopes = ConditionScopeFlags.Hook | ConditionScopeFlags.FishIgnore | ConditionScopeFlags.AutoCast,
            Evaluate = (w, p) =>
            {
                var wanted = GetInt(p, "zone", 0);
                var zone = (int)w.OceanFishing.CurrentZone;
                var result = zone == wanted;
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
            Description = "Compares current swimbait count against a value.",
            AllowedScopes = ConditionScopeFlags.Hook | ConditionScopeFlags.AutoCast,
            Evaluate = (w, p) =>
            {
                var val = GetInt(p, "val", 0);
                var op = GetOp(p, "op", ">=");
                var count = w.GetSwimbaitCount();
                var result = CompareInt(count, val, op);
                return GetBool(p, "inv", false) ? !result : result;
            },
        });

        // ---- Last ocean fish points (current zone) ----
        // Params: "val" = points threshold, "op" = >,>=,<,<=,=, "inv" = optional
        // Uses FishData for current zone; last non-zero ItemId entry's points per fish (TotalPoints / (Nq+Hq)).
        registry.Register(new ConditionTypeDef
        {
            Id = ConditionId.OceanLastFishPoints,
            Name = "Last ocean fish points",
            Category = "Fishing",
            Description = "Compares the points value of the last caught ocean fish in the current zone against a value.",
            AllowedScopes = ConditionScopeFlags.Hook | ConditionScopeFlags.FishIgnore | ConditionScopeFlags.AutoCast,
            Evaluate = (w, p) =>
            {
                var points = GetLastOceanFishPointsValue(w);
                if (points == null) return GetBool(p, "inv", false);
                var val = GetInt(p, "val", 0);
                var op = GetOp(p, "op", ">=");
                var result = CompareInt(points.Value, val, op);
                return GetBool(p, "inv", false) ? !result : result;
            },
        });
    }

    /// <summary>Points per fish for the last (most recent) fish entry in the current ocean fishing zone, or null if not applicable.</summary>
    private static int? GetLastOceanFishPointsValue(WorldState w)
    {
        var of = w.OceanFishing;
        if (of.FishData == null || of.FishData.Count < 60) return null;
        var zone = (int)Math.Clamp(of.CurrentZone, 0, 2);
        var start = zone * 20;
        for (var i = start + 19; i >= start; i--)
        {
            var f = of.FishData[i];
            if (f.ItemId == 0) continue;
            var count = f.NqAmount + f.HqAmount;
            if (count == 0) return (int)f.TotalPoints;
            return (int)(f.TotalPoints / count);
        }
        return null;
    }

    private static List<uint> GetIds(IReadOnlyDictionary<string, object> p)
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
