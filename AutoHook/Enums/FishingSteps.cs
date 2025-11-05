namespace AutoHook.Enums;

[Flags]
public enum FishingSteps
{
    None = 0x01,
    BeganFishing = 0x02,
    BeganMooching = 0x04,
    FishBit = 0x08,
    Reeling = 0x10,
    FishCaught = 0x20,
    BaitSwapped = 0x40,
    PresetSwapped = 0x80,
    FishReeled = 0x100,
    TimeOut = 0x200,
    Quitting = 0x400,
    StartedCasting = 0x800,
}