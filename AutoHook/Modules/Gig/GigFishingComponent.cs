namespace AutoHook.Modules.Gig;

public abstract class GigFishingComponent(GigFishingModule module) : FishingComponent(module) {
    protected GigFishingModule Gig { get; } = module;
}
