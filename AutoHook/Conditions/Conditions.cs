namespace AutoHook.Conditions;

/// <summary>
/// Shared registry for condition evaluation. WorldState is the single instance in <see cref="AutoHook.Service.WorldState"/>.
/// </summary>
public static class Conditions
{
    public static ConditionRegistry Registry { get; } = new();

    static Conditions()
    {
        ConditionTypes.RegisterAll(Registry);
    }
}
