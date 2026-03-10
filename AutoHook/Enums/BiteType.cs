using FFXIVClientStructs.FFXIV.Client.Game.Event;

namespace AutoHook.Enums;

public enum BiteType : byte {
    Unknown = 0,
    Weak = 36,
    Strong = 37,
    Legendary = 38,
    None = 255,
}

public static class FishingHookStrengthExtensions {
    public static BiteType ToBiteType(this FishingHookStrength biteType) => biteType switch {
        FishingHookStrength.Weak => BiteType.Weak,
        FishingHookStrength.Strong => BiteType.Strong,
        FishingHookStrength.Legendary => BiteType.Legendary,
        _ => BiteType.Unknown,
    };
}
