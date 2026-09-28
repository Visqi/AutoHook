namespace AutoHook.World.UpdateModules;

public readonly struct BiteContext {
    public double BiteTimeSeconds { get; init; }
    public bool ChumActive { get; init; }
    public IntuitionStatus IntuitionStatus { get; init; }
    public float IntuitionTimeRemaining { get; init; }
    public SpectralCurrentStatus SpectralCurrentStatus { get; init; }
    public uint? LastCaughtFishId { get; init; }
}
