using FFXIVClientStructs.FFXIV.Client.Game.WKS;

namespace AutoHook.World.UpdateModules;

public sealed class WksUpdateModule : IWorldUpdateModule {
    public void Update(WorldState ws) {
        var next = CollectWKSInfo();
        var w = ws.WKS;
        if (w.DevGrade == next.DevGrade && w.CurrentFateControlRowId == next.CurrentFateControlRowId &&
            w.CurrentFateId == next.CurrentFateId && w.CurrentMissionUnitRowId == next.CurrentMissionUnitRowId &&
            w.CurrentScore == next.CurrentScore && w.CurrentRank == next.CurrentRank &&
            w.CollectedTotal == next.CollectedTotal && w.CollectedIndividual == next.CollectedIndividual)
            return;
        ws.Execute(next);
    }

    private static unsafe WksState.OpState CollectWKSInfo() {
        ushort devGrade = 0;
        ushort currentFateControlRowId = 0;
        ushort currentFateId = 0;
        ushort currentMissionUnitRowId = 0;
        uint currentScore = 0;
        var currentRank = WKSMissionModule.MissionRank.None;
        ushort collectedTotal = 0;
        byte collectedIndividual = 0;

        try {
            if (Player.Territory is { Value.TerritoryIntendedUse.RowId: 60 } && WKSManager.Instance() is not null and var wks) {
                devGrade = wks->State.DevGrade;
                currentFateControlRowId = wks->State.CurrentFateControlRowId;
                currentFateId = wks->State.CurrentFateId;
                currentMissionUnitRowId = wks->State.CurrentMission.MissionUnitRowId;
                currentScore = wks->State.CurrentMission.ScoreUInt;
                currentRank = wks->State.CurrentMission.Rank;
                collectedTotal = wks->State.CurrentMission.CollectedTotal;
                collectedIndividual = wks->State.CurrentMission.CollectedIndividual;
            }
        }
        catch { }

        return new WksState.OpState(devGrade, currentFateControlRowId, currentFateId, currentMissionUnitRowId, currentScore, currentRank, collectedTotal, collectedIndividual);
    }
}
