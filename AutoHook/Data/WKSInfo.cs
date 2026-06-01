using FFXIVClientStructs.FFXIV.Client.Game.WKS;

namespace AutoHook.Data;

public sealed class WKSInfo {
    public ushort DevGrade;
    public ushort CurrentFateControlRowId;
    public ushort CurrentFateId;
    public ushort CurrentMissionUnitRowId;
    public uint CurrentScore;
    public WKSManager.MissionRank CurrentRank;
    public ushort CollectedTotal;
    public byte CollectedIndividual;

    public IEnumerable<WorldState.Operation> CompareToInitial() {
        if (DevGrade != 0 || CurrentFateControlRowId != 0 || CurrentFateId != 0 ||
            CurrentMissionUnitRowId != 0 || CurrentScore != 0 || CurrentRank != WKSManager.MissionRank.None ||
            CollectedTotal != 0 || CollectedIndividual != 0) {
            yield return new OpState(DevGrade, CurrentFateControlRowId, CurrentFateId, CurrentMissionUnitRowId, CurrentScore, CurrentRank, CollectedTotal, CollectedIndividual);
        }
    }

    public sealed record OpState(ushort DevGrade, ushort CurrentFateControlRowId, ushort CurrentFateId, ushort CurrentMissionUnitRowId, uint CurrentScore, WKSManager.MissionRank CurrentRank, ushort CollectedTotal, byte CollectedIndividual) : WorldState.Operation {
        protected override void Exec(WorldState ws) {
            ws.WKS.DevGrade = DevGrade;
            ws.WKS.CurrentFateControlRowId = CurrentFateControlRowId;
            ws.WKS.CurrentFateId = CurrentFateId;
            ws.WKS.CurrentMissionUnitRowId = CurrentMissionUnitRowId;
            ws.WKS.CurrentScore = CurrentScore;
            ws.WKS.CurrentRank = CurrentRank;
            ws.WKS.CollectedTotal = CollectedTotal;
            ws.WKS.CollectedIndividual = CollectedIndividual;
        }
    }
}
