using System.Reflection;

namespace AutoHook.Conditions;

public class ConditionRegistry {
    public static ConditionRegistry Registry { get; } = new();

    static ConditionRegistry() => Assembly.GetExecutingAssembly().GetTypes().Where(t => !t.IsAbstract && typeof(IConditionDefinition).IsAssignableFrom(t))
        .Select(t => Activator.CreateInstance(t) as IConditionDefinition)
        .Where(def => def != null)
        .ForEach(def => Registry.Register(def!.ToTypeDef()));

    private ConditionRegistry() { }

    private readonly Dictionary<string, ConditionTypeDef> _byId = [];
    private readonly Dictionary<Type, string> _idByType = [];
    private readonly Dictionary<Type, ConditionTypeDef> _byType = [];

    public void Register(ConditionTypeDef def) {
        if (string.IsNullOrEmpty(def.Id)) return;
        _byId[def.Id] = def;
        if (def.Definition != null)
            _byType[def.Definition.GetType()] = def;
    }

    public ConditionTypeDef? Get(string typeId) => _byId.TryGetValue(typeId, out var d) ? d : null;
    public T? GetDefinition<T>() where T : class => _byType.TryGetValue(typeof(T), out var d) ? d.Definition as T : null;

    public IReadOnlyCollection<ConditionTypeDef> All => _byId.Values;

    public string GetId(Type type) {
        if (_idByType.TryGetValue(type, out var id)) return id;
        if (Activator.CreateInstance(type) is not IConditionDefinition def) return string.Empty;
        _idByType[type] = def.Id;
        return def.Id;
    }

    public string GetId<T>() where T : IConditionDefinition => GetId(typeof(T));
}

public class ConditionTypeDef {
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public ConditionScopeFlags AllowedScopes { get; init; } = ConditionScopeFlags.All;
    public Action<Condition>? DrawParams { get; init; }
    public Func<WorldState, IReadOnlyDictionary<string, object>, bool> Evaluate { get; init; } = (_, _) => false;
    public IConditionDefinition? Definition { get; init; }
}

[Flags]
public enum ConditionScopeFlags {
    None = 0,
    Hook = 1 << 0,
    AutoCordial = 1 << 1,
    FishIgnore = 1 << 2,
    AutoCast = 1 << 3,
    All = Hook | AutoCordial | FishIgnore | AutoCast,
}
