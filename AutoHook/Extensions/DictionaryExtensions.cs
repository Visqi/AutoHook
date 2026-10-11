namespace AutoHook.Extensions;

public static class DictionaryExtensions {
    extension<TKey, TValue>(IDictionary<TKey, TValue> dict) {
        public string JoinValues() => dict.Count == 0 ? string.Empty : string.Join(", ", dict.Select(kv => kv.Value?.ToString()));
    }
}
