using Lumina.Excel.Sheets;

namespace AutoHook.Data;

public readonly record struct OceanStopKey(uint SpotId, uint TimeId);

public static class OceanStopUtil {
    public static string FormatStopLabel(uint spotId, uint timeId) {
        var spotName = GetSpotName(spotId);
        var todLabel = (TimeOfDay)timeId switch {
            TimeOfDay.Day => "Day",
            TimeOfDay.Sunset => "Sunset",
            TimeOfDay.Night => "Night",
            _ => timeId.ToString(),
        };
        return $"{spotName} ({todLabel})";
    }

    public static IEnumerable<OceanStopKey> GetUniqueStops() {
        var seen = new HashSet<(uint SpotId, uint TimeId)>();
        foreach (var route in Svc.Data.GetExcelSheet<IKDRoute>()) {
            if (route.RowId == 0)
                continue;
            for (var z = 0; z < 3; z++) {
                var spotId = route.Spot[z].RowId;
                var timeId = route.Time[z].RowId;
                if (spotId == 0 && timeId == 0)
                    continue;
                if (seen.Add((spotId, timeId)))
                    yield return new OceanStopKey(spotId, timeId);
            }
        }
    }

    public static bool MatchesStop(uint spotId, uint timeId, OceanFishingState ocean) {
        return spotId != 0 && timeId != 0 && ocean.CurrentSpotId == spotId && ocean.CurrentTimeId == timeId;
    }

    public static string FormatStateLog(OceanFishingState ocean) {
        var stop = ocean.CurrentSpotId != 0 && ocean.CurrentTimeId != 0
            ? FormatStopLabel(ocean.CurrentSpotId, ocean.CurrentTimeId)
            : "-";
        return $"zone={ocean.CurrentZone + 1}, stop={stop}, route={ocean.CurrentRoute}, status={ocean.Status}, " +
               $"timer={ocean.TimeLeftInZone:F1}/{ocean.ZoneTimeMax:F1}s, spectral={ocean.SpectralCurrentActive}";
    }

    private static string GetSpotName(uint spotId) {
        if (spotId == 0)
            return "Unknown";
        if (!IKDSpot.TryGetRow(spotId, out var spot))
            return $"Spot #{spotId}";
        var name = spot.PlaceName.Value.Name.ToString();
        return string.IsNullOrEmpty(name) ? $"Spot #{spotId}" : name;
    }
}
