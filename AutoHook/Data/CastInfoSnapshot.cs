using FFXIVClientStructs.FFXIV.Client.System.Framework;
using Lumina.Excel.Sheets;

namespace AutoHook.Data;

// since some values are snapshotted on the moment you cast, whether or not they expire by the time you hook doesn't matter so for eval purposes we should check against a snapshot and not live values
public sealed class CastInfoSnapshot {
    public bool Active { get; private set; }

    public IntuitionStatus IntuitionStatus { get; private set; }
    public byte CurrentWeatherId { get; private set; }
    public byte PreviousWeatherId { get; private set; }
    public byte NextWeatherId { get; private set; }
    public TimeOnly EorzeaTime { get; private set; }
    public SpectralCurrentStatus SpectralCurrentStatus { get; private set; }

    public unsafe void Capture(WorldState ws) {
        byte previousWeatherId = 0;
        byte nextWeatherId = 0;

        var territorySheet = Svc.Data.GetExcelSheet<TerritoryType>();
        if (ws.TerritoryId != 0 && territorySheet.TryGetRow(ws.TerritoryId, out var territory)) {
            previousWeatherId = (byte)territory.GetPreviousWeather().RowId;
            nextWeatherId = (byte)territory.GetNextWeather().RowId;
        }

        EorzeaTime = TimeOnly.FromDateTime(DateTimeOffset.FromUnixTimeSeconds(Framework.Instance()->ClientTime.EorzeaTime).DateTime);
        IntuitionStatus = ws.Fishing.Intuition.Status;
        CurrentWeatherId = ws.CurrentWeatherId;
        PreviousWeatherId = previousWeatherId;
        NextWeatherId = nextWeatherId;
        SpectralCurrentStatus = ws.SpectralCurrentStatus;
        Active = true;
    }

    public void Invalidate() => Active = false;
}
