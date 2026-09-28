namespace AutoHook.Modules;

public abstract class FishingModule(WorldState worldState) : IDisposable {
    public WorldState WorldState { get; } = worldState;

    private readonly List<FishingComponent> _components = [];
    public IReadOnlyList<FishingComponent> Components => _components;

    protected T AddComponent<T>(T component) where T : FishingComponent {
        _components.Add(component);
        return component;
    }

    public T? FindComponent<T>() where T : FishingComponent
        => _components.OfType<T>().FirstOrDefault();

    public virtual void Update() {
        foreach (var c in _components)
            c.Update();
    }

    public virtual void Dispose() { }
}
