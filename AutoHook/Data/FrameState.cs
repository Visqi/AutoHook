namespace AutoHook.Data;

public record struct FrameState(DateTime Timestamp, ulong QPC, uint Index, float DurationRaw, float Duration, float TickSpeedMultiplier);

