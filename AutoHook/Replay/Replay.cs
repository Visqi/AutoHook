namespace AutoHook.Replay;

public sealed class FishingReplay {
    public ulong QPF = TimeSpan.TicksPerSecond;
    public string GameVersion = string.Empty;
    public string SourcePath = string.Empty;
    public ReplayMetadata Metadata = new();
    public List<WorldState.Operation> Ops { get; } = [];

    public IReadOnlyList<WorldState.OpDecision> Decisions
        => [.. Ops.OfType<WorldState.OpDecision>()];

    public DateTime StartTime => Ops.Count > 0 ? Ops[0].Timestamp : default;
    public DateTime EndTime => Ops.Count > 0 ? Ops[^1].Timestamp : default;
}

public sealed class ReplayMetadata {
    public string PresetName = string.Empty;
    public string PluginVersion = string.Empty;
    public uint TerritoryId;
    public string PresetSnapshotJson = string.Empty;
}
