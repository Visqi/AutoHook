namespace AutoHook.Utils;

public static class SignaturePatterns
{
    // Used to hook ExecuteCommand (bait/swimbait changes, etc.)
    public const string ExecuteCommand = "E8 ?? ?? ?? ?? 41 C6 04 24";

    // Used to hook UpdateCatch in FishingManager
    public const string UpdateCatch =
        "48 89 6C 24 ?? 56 41 56 41 57 48 81 EC ?? ?? ?? ?? 48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 84 24 ?? ?? ?? ?? 48 8B 01";

    // Used by SeTugType to read current tug/bite type
    public const string TugType = "48 8D 35 ?? ?? ?? ?? 4C 8B CE";
}
