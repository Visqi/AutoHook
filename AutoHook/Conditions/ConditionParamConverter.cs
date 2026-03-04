using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AutoHook.Conditions;

/// <summary>
/// Minimal serialization for condition parameters:
/// - Skips nulls
/// - Skips default/empty values (false, 0, empty lists)
/// This keeps ConditionSet JSON small and stable.
/// </summary>
public class ConditionParamConverter : JsonConverter<Dictionary<string, object>>
{
    public override void WriteJson(JsonWriter writer, Dictionary<string, object>? value, JsonSerializer serializer)
    {
        if (value == null || value.Count == 0)
        {
            writer.WriteStartObject();
            writer.WriteEndObject();
            return;
        }

        // Filter out default / empty values
        var filtered = new Dictionary<string, object>();
        foreach (var (key, rawVal) in value)
        {
            if (rawVal == null)
                continue;

            switch (rawVal)
            {
                case bool b when !b:
                    continue;
                case int i when i == 0:
                    continue;
                case long l when l == 0:
                    continue;
                case double d when Math.Abs(d) < double.Epsilon:
                    continue;
                case float f when Math.Abs(f) < float.Epsilon:
                    continue;
                case IList<object> list when list.Count == 0:
                    continue;
            }

            filtered[key] = rawVal;
        }

        // If everything was default, still emit an empty object
        if (filtered.Count == 0)
        {
            writer.WriteStartObject();
            writer.WriteEndObject();
            return;
        }

        serializer.Serialize(writer, filtered);
    }

    public override Dictionary<string, object> ReadJson(JsonReader reader, Type objectType, Dictionary<string, object>? existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        if (reader.TokenType == JsonToken.Null)
            return [];

        var token = JToken.Load(reader);
        if (token.Type == JTokenType.Object)
        {
            var dict = new Dictionary<string, object>();
            foreach (var prop in token.Children<JProperty>())
            {
                dict[prop.Name] = ConvertToken(prop.Value);
            }

            return dict;
        }

        return [];
    }

    private static object ConvertToken(JToken token) => token.Type switch
    {
        JTokenType.Array => token.Children().Select(ConvertToken).ToList(),
        JTokenType.Integer => (long)token,
        JTokenType.Float => (double)token,
        JTokenType.Boolean => (bool)token,
        JTokenType.String => (string)token,
        _ => token.ToString()
    };
}

