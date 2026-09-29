using FFXIVClientStructs.FFXIV.Client.Game.UI;
using AchievementSheet = Lumina.Excel.Sheets.Achievement;

namespace AutoHook.World;

public static class AchievementProgressSnapshot {
    public static unsafe List<WorldState.OpAchievementProgress> Collect() {
        var results = new List<WorldState.OpAchievementProgress>();
        var ach = Achievement.Instance();
        if (ach == null || !ach->IsLoaded())
            return results;

        var ws = WorldState.Get();
        var emitted = new HashSet<uint>();

        foreach (var (id, progress) in ws.AchievementProgress) {
            if (progress.Max == 0)
                continue;
            emitted.Add(id);
            results.Add(new WorldState.OpAchievementProgress(id, progress.Current, progress.Max));
        }

        foreach (var row in AchievementSheet.Where(r => r.RowId != 0 && !emitted.Contains(r.RowId))) {
            if (ach->IsComplete((int)row.RowId))
                results.Add(new WorldState.OpAchievementProgress(row.RowId, 1, 1));
        }

        return results;
    }
}
