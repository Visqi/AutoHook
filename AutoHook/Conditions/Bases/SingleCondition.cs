using Newtonsoft.Json;

namespace AutoHook.Conditions.Binding;

public sealed class SingleCondition<TCD, TValue>(Func<object?>? context = null) where TCD : class, IConditionDefinition, ISimpleConditionValue<TValue> where TValue : struct {
    // serialized as ConditionSet via SingleConditionConverter.
    public ConditionSet? BackingSet { get; set; }

    private ISimpleConditionValue<TValue> Definition => field ??= ConditionRegistry.Registry.GetDefinition<TCD>()!;

    public TValue Value {
        get => Get();
        set => Set(value);
    }

    private TValue Get() {
        var set = BackingSet;
        var cond = set.GetFirstCondition(Definition.Id);
        return cond == null ? default : Definition.FromParams(cond.Params);
    }

    private void Set(TValue value) {
        var p = Definition.ToParams(value, context?.Invoke());
        if (p == null || p.Count == 0) {
            var set = BackingSet;
            set = ConditionSetUtil.SetSingleCondition(set, Definition.Id, null);
            BackingSet = ConditionSetUtil.CompactOrNull(set);
        }
        else {
            BackingSet = ConditionSetUtil.SetSingleCondition(BackingSet, Definition.Id, p);
        }
    }
}

// serializes as ConditionSet only — same JSON as ConditionSet?.
public sealed class SingleConditionConverter : JsonConverter {
    public override bool CanConvert(Type objectType)
        => objectType.IsGenericType && objectType.GetGenericTypeDefinition() == typeof(SingleCondition<,>);

    public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer) {
        var set = value == null ? null : GetBackingSet(value);
        serializer.Serialize(writer, set);
    }

    public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer) {
        var set = serializer.Deserialize<ConditionSet>(reader);
        var instance = existingValue ?? Activator.CreateInstance(objectType);
        if (instance != null)
            objectType.GetProperty("BackingSet")!.SetValue(instance, set);
        return instance;
    }

    private static ConditionSet? GetBackingSet(object value)
        => (ConditionSet?)value.GetType().GetProperty("BackingSet")!.GetValue(value);
}
