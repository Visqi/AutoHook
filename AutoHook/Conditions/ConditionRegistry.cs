namespace AutoHook.Conditions;

public class ConditionTypeDef
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Category { get; init; } = "";
    public string Description { get; init; } = "";
    public ConditionScopeFlags AllowedScopes { get; init; } = ConditionScopeFlags.All;
    public Func<WorldState, IReadOnlyDictionary<string, object>, bool> Evaluate { get; init; } = (_, _) => false;
}

public class ConditionRegistry
{
    private readonly Dictionary<string, ConditionTypeDef> _byId = [];

    public void Register(ConditionTypeDef def)
    {
        if (string.IsNullOrEmpty(def.Id)) return;
        _byId[def.Id] = def;
    }

    public ConditionTypeDef? Get(string typeId) => _byId.TryGetValue(typeId, out var d) ? d : null;

    public IReadOnlyCollection<ConditionTypeDef> All => _byId.Values;
}

[Flags]
public enum ConditionScopeFlags
{
    None = 0,
    Hook = 1 << 0,
    AutoCordial = 1 << 1,
    FishIgnore = 1 << 2,
    AutoCast = 1 << 3,
    All = Hook | AutoCordial | FishIgnore | AutoCast,
}
