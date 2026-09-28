using Dalamud.Game.ClientState.Objects.SubKinds;
using FFXIVClientStructs.FFXIV.Client.Game.Event;
using FFXIVClientStructs.FFXIV.Client.Game.InstanceContent;
using Lumina.Excel.Sheets;

namespace AutoHook.World.UpdateModules;

public sealed class OceanUpdateModule : IWorldUpdateModule {
    private readonly List<InstanceContentOceanFishing.FishDataStruct> _fishDataScratch = [];

    public unsafe void Update(WorldState ws) {
        var ptr = EventFramework.Instance()->GetInstanceContentOceanFishing();
        if (ptr == null) {
            if (ws.Ocean.OceanFishing != OceanFishingState.Empty)
                ws.Execute(new OceanState.OpOceanFishing(null));
            return;
        }

        _fishDataScratch.Clear();
        foreach (var f in ptr->FirstZoneFishData)
            _fishDataScratch.Add(f);
        foreach (var f in ptr->SecondZoneFishData)
            _fishDataScratch.Add(f);
        foreach (var f in ptr->ThirdZoneFishData)
            _fishDataScratch.Add(f);

        var routeRow = IKDRoute.GetRow(ptr->CurrentRoute);
        var zoneIndex = (int)ptr->CurrentZone;
        var timeId = routeRow.Time[zoneIndex].RowId;
        var state = new OceanFishingState {
            SpectralCurrentActive = ptr->SpectralCurrentActive,
            CurrentRoute = ptr->CurrentRoute,
            TimeOfDay = (TimeOfDay)timeId,
            CurrentZone = ptr->CurrentZone,
            CurrentSpotId = routeRow.Spot[zoneIndex].RowId,
            CurrentTimeId = timeId,
            TimeLeftInZone = Math.Max(0f, EventFramework.Instance()->GetInstanceContentDirector()->ContentTimeLeft - ptr->TimeOffset),
            ZoneTimeMax = ptr->Duration,
            Mission1 = new OceanMission(ptr->Mission1Type, ptr->Mission1Progress),
            Mission2 = new OceanMission(ptr->Mission2Type, ptr->Mission2Progress),
            Mission3 = new OceanMission(ptr->Mission3Type, ptr->Mission3Progress),
            PlayerCount = IObjectTable.Get().OfType<IPlayerCharacter>().Count(),
            FishData = [.. _fishDataScratch],
            Status = ptr->Status,
        };

        if (ws.Ocean.OceanFishing.SameAs(state))
            return;

        ws.Execute(new OceanState.OpOceanFishing(state));
    }
}
