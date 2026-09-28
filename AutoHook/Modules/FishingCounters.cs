namespace AutoHook.Modules;

public static class FishingCounters {
    private static Dictionary<Guid, int> FishCount = [];
    private static List<Guid> FishPresetSwapped = [];
    private static List<Guid> FishBaitSwapped = [];
    private static readonly List<Guid> ToBeRemoved = [];

    public static void AddFishCount(Guid guid) {
        FishCount.TryAdd(guid, 0);
        FishCount[guid]++;
    }

    public static void AddBaitSwap(Guid guid) {
        if (!FishBaitSwapped.Contains(guid))
            FishBaitSwapped.Add(guid);
    }

    public static void AddPresetSwap(Guid guid) {
        if (!FishPresetSwapped.Contains(guid))
            FishPresetSwapped.Add(guid);
    }

    public static void RemovePresetSwap(Guid guid) {
        if (SwappedPreset(guid))
            FishPresetSwapped.Remove(guid);
    }

    public static int GetFishCount(Guid guid)
        => !FishCount.TryGetValue(guid, out var value) ? 0 : value;

    public static bool SwappedBait(Guid guid)
        => FishBaitSwapped.Any(g => g == guid);

    public static bool SwappedPreset(Guid guid)
        => FishPresetSwapped.Any(g => g == guid);

    public static void RemoveId(Guid guid) {
        FishCount.Remove(guid);
        if (SwappedPreset(guid))
            FishPresetSwapped.Remove(guid);
        if (SwappedBait(guid))
            FishBaitSwapped.Remove(guid);
    }

    public static void QueueRemove(Guid guid) {
        if (!ToBeRemoved.Contains(guid))
            ToBeRemoved.Add(guid);
    }

    public static void RemoveGuidQueue() {
        foreach (var guid in ToBeRemoved)
            RemoveId(guid);
        ToBeRemoved.Clear();
    }

    public static void Reset() {
        FishCount = [];
        FishPresetSwapped = [];
        FishBaitSwapped = [];
        ToBeRemoved.Clear();
    }
}
