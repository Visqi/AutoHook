using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace AutoHook.Utils;

public static class IKDResultHandler {
    public static void Enable() => Svc.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "IKDResult", OnResultsSetup);
    public static void Disable() => Svc.AddonLifecycle.UnregisterListener(OnResultsSetup);
    private static unsafe void OnResultsSetup(AddonEvent type, AddonArgs args) {
        if (Service.Configuration.AutoOceanFish)
            args.GetAddon<AtkUnitBase>()->Close(true);
    }
}
