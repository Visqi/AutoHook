namespace AutoHook.Fishing;

/// <summary>
/// Auto-closes the ocean fishing results addon at voyage end.
/// </summary>
/// <remarks>TODO: register AddonLifecycle listener once the results addon name is confirmed (likely IKDResult).</remarks>
public static class OceanFishingResultsHandler {
    public static void Enable() {
        // Pin: Svc.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "IKDResult", OnResultsSetup);
    }

    public static void Disable() {
        // Pin: Svc.AddonLifecycle.UnregisterListener(...);
    }
}
