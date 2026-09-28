namespace AutoHook.Modules.Rod;

public abstract class RodComponent(RodFishingModule module) : FishingComponent(module) {
    protected RodFishingModule Rod { get; } = module;
}
