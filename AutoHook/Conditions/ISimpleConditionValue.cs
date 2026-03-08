namespace AutoHook.Conditions;

/// <summary>
/// Optional interface for condition definitions that support a "simple value" binding (one typed value backed by a single condition in a set)
/// </summary>
public interface ISimpleConditionValue<T> : IConditionDefinition where T : struct
{
    T FromParams(IReadOnlyDictionary<string, object> p);
    IReadOnlyDictionary<string, object>? ToParams(T value, object? context = null);
}
