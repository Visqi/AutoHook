namespace AutoHook.Actions;

/// <summary>Numeric priority bands for cast hints. Higher wins.</summary>
public static class HintPriority {
    public const int SpectralRest = 1000;
    public const int TimeoutRest = 990;
    public const int Hook = 950;
    public const int FishCaughtAction = 900;
    public const int FishCaughtMultiHook = 880;
    public const int GpRestore = 800;
    public const int AutoCast = 700;
    public const int CollectBeforeLine = 650;
    public const int Mooch = 620;
    public const int CastLine = 600;
    public const int GigSession = 500;
    public const int GigNaturesBounty = 480;
    public const int Gig = 450;

    public static int ForAutoCast(BaseActionCast action)
        => AutoCast + action.GetPriority();

    public static int ForGigAutoCast(BaseActionCast action)
        => GigSession + action.GetPriority();
}
