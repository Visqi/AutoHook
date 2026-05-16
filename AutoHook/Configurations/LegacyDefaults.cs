using System.Collections;
using System.ComponentModel;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AutoHook.Configurations;

// this class exists because I fucked up--old exports dropped default bools
// if code default was true and the key is absent, force false (what those old exports meant)
internal static class LegacyDefaults {
    private const BindingFlags Flags = BindingFlags.Public | BindingFlags.Instance;
    private static readonly Dictionary<MemberInfo, bool> _trueDefaultsCache = [];

    public static void Apply(JToken token, object target) {
        if (token is JObject jo)
            ApplyObject(jo, target);
        else if (token is JArray ja && target is IList list)
            ApplyArray(ja, list);
    }

    private static void ApplyObject(JObject jo, object target) {
        foreach (var member in GetBoolMembers(target.GetType())) {
            if (jo.ContainsKey(GetJsonName(member)) || !GetTrueDefault(member))
                continue;
            SetMember(member, target, false);
        }

        foreach (var member in EnumerateMembers(target.GetType())) {
            var value = GetMember(member, target);
            if (value == null || !jo.TryGetValue(GetJsonName(member), out var child))
                continue;
            Apply(child, value);
        }
    }

    private static void ApplyArray(JArray ja, IList list) {
        for (var i = 0; i < list.Count && i < ja.Count; i++) {
            if (list[i] is { } item)
                Apply(ja[i], item);
        }
    }

    private static IEnumerable<MemberInfo> GetBoolMembers(Type type) {
        foreach (var member in EnumerateMembers(type)) {
            var memberType = member is PropertyInfo p ? p.PropertyType : ((FieldInfo)member).FieldType;
            if (memberType == typeof(bool) && (member is not PropertyInfo prop || prop.CanWrite))
                yield return member;
        }
    }

    private static IEnumerable<MemberInfo> EnumerateMembers(Type type) {
        foreach (var prop in type.GetProperties(Flags)) {
            if (prop.GetIndexParameters().Length == 0)
                yield return prop;
        }

        foreach (var field in type.GetFields(Flags)) {
            if (!field.IsLiteral)
                yield return field;
        }
    }

    private static string GetJsonName(MemberInfo member) {
        var name = member.GetCustomAttribute<JsonPropertyAttribute>()?.PropertyName;
        return string.IsNullOrEmpty(name) ? member.Name : name;
    }

    private static bool GetTrueDefault(MemberInfo member) {
        if (_trueDefaultsCache.TryGetValue(member, out var cached))
            return cached;

        if (member.GetCustomAttribute<DefaultValueAttribute>()?.Value is bool attrValue) {
            _trueDefaultsCache[member] = attrValue;
            return attrValue;
        }

        var type = member.DeclaringType;
        var instance = type != null ? TryCreateInstance(type) : null;
        var isTrue = instance != null && GetMember(member, instance) is true;
        _trueDefaultsCache[member] = isTrue;
        return isTrue;
    }

    // some config types need ctor args
    private static object? TryCreateInstance(Type type) {
        try {
            if (type == typeof(BaseBiteConfig))
                return new BaseBiteConfig(HookType.Normal);
            if (type == typeof(BaseHookset))
                return new BaseHookset(0);
            return Activator.CreateInstance(type);
        }
        catch {
            return null;
        }
    }

    private static object? GetMember(MemberInfo member, object target) => member switch {
        PropertyInfo p => p.GetValue(target),
        FieldInfo f => f.GetValue(target),
        _ => null
    };

    private static void SetMember(MemberInfo member, object target, bool value) {
        switch (member) {
            case PropertyInfo p: p.SetValue(target, value); break;
            case FieldInfo f: f.SetValue(target, value); break;
        }
    }
}
