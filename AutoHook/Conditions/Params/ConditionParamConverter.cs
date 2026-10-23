using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AutoHook.Conditions.Params;

public class ConditionParamConverter : JsonConverter<Dictionary<string, object>> {
    [ThreadStatic] private static Stack<IConditionDefinition?>? _definitions;

    internal static void PushDefinition(IConditionDefinition? definition) {
        _definitions ??= new Stack<IConditionDefinition?>();
        _definitions.Push(definition);
    }

    internal static void PopDefinition() {
        if (_definitions is { Count: > 0 })
            _definitions.Pop();
    }

    public override void WriteJson(JsonWriter writer, Dictionary<string, object>? value, JsonSerializer serializer) {
        if (value == null || value.Count == 0) {
            writer.WriteStartObject();
            writer.WriteEndObject();
            return;
        }

        var definition = _definitions is { Count: > 0 } ? _definitions.Peek() : null;
        var filtered = new Dictionary<string, object>();
        foreach (var (key, rawVal) in value) {
            if (ShouldOmit(key, rawVal, definition))
                continue;

            filtered[key] = rawVal;
        }

        // If everything was default, still emit an empty object
        if (filtered.Count == 0) {
            writer.WriteStartObject();
            writer.WriteEndObject();
            return;
        }

        serializer.Serialize(writer, filtered);
    }

    private static bool ShouldOmit(string key, object? rawVal, IConditionDefinition? definition) {
        if (rawVal == null)
            return true;

        if (rawVal is IList<object> list && list.Count == 0)
            return true;

        if (!IsNumericZero(rawVal))
            return false;

        var specified = definition?.SpecifiedIntDefault(key);
        return specified is not int value || value == 0;
    }

    private static bool IsNumericZero(object rawVal) => rawVal switch {
        int i => i == 0,
        long l => l == 0,
        double d => Math.Abs(d) < double.Epsilon,
        float f => Math.Abs(f) < float.Epsilon,
        _ => false
    };

    public override Dictionary<string, object> ReadJson(JsonReader reader, Type objectType, Dictionary<string, object>? existingValue, bool hasExistingValue, JsonSerializer serializer) {
        if (reader.TokenType == JsonToken.Null)
            return [];

        var token = JToken.Load(reader);
        if (token.Type == JTokenType.Object) {
            var dict = new Dictionary<string, object>();
            foreach (var prop in token.Children<JProperty>()) {
                dict[prop.Name] = ConvertToken(prop.Value);
            }

            return dict;
        }

        return [];
    }

    private static object ConvertToken(JToken token) => token.Type switch {
        JTokenType.Array => token.Children().Select(ConvertToken).ToList(),
        JTokenType.Integer => (long)token,
        JTokenType.Float => (double)token,
        JTokenType.Boolean => (bool)token,
        JTokenType.String => (string?)token ?? string.Empty,
        _ => token.ToString()
    };
}

