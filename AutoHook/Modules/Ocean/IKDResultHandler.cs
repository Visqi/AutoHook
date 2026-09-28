using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace AutoHook.Modules.Ocean;

public static class IKDResultHandler {
    public static void Enable() => IAddonLifecycle.Get().RegisterListener(AddonEvent.PostSetup, "IKDResult", OnResultsSetup);
    public static void Disable() => IAddonLifecycle.Get().UnregisterListener(OnResultsSetup);
    private static unsafe void OnResultsSetup(AddonEvent type, AddonArgs args) {
        if (Service.Configuration.AutoOceanFish)
            args.GetAddon<AtkUnitBase>()->Close(true);
    }
}
