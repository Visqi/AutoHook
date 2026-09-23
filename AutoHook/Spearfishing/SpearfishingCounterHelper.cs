namespace AutoHook.Spearfishing;

public static class SpearfishingCounterHelper {
    private static readonly Dictionary<Guid, int> FishCount = [];

    public static int GetFishCount(Guid id)
        => FishCount.GetValueOrDefault(id);

    public static void AddFishCount(Guid id, int amount) {
        if (id == Guid.Empty || amount <= 0)
            return;

        FishCount[id] = FishCount.GetValueOrDefault(id) + amount;
    }

    public static void Remove(Guid id)
        => FishCount.Remove(id);

    public static void Reset(IEnumerable<BaseGig> gigs) {
        foreach (var gig in gigs)
            FishCount.Remove(gig.UniqueId);
    }

    public static void ResetAll()
        => FishCount.Clear();
}
