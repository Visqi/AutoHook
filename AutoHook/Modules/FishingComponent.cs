namespace AutoHook.Modules;

public abstract class FishingComponent(FishingModule module) {
    public FishingModule Module { get; } = module;
    protected WorldState Ws => Module.WorldState;

    public virtual void Update() { }

    public virtual void ContributeHints(ActionHints hints) { }
}
