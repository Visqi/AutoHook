using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using FFXIVClientStructs.FFXIV.Client.System.Framework;

namespace AutoHook.Conditions.Definitions;

public sealed class TimeWindowCD : IConditionDefinition, ISimpleConditionValue<(bool Enabled, TimeOnly Start, TimeOnly End)>
{
    public string Id => nameof(TimeWindowCD);
    public string Name => "Time window";
    public string Category => "Time";
    public string Description => "Checks current Eorzea time between start and end.";
    public ConditionScopeFlags AllowedScopes => ConditionScopeFlags.AutoCast;

    public readonly record struct TimeWindowParams(TimeOnly Start, TimeOnly End, bool Invert)
    {
        public Dictionary<string, object> ToParams()
        {
            var dict = new Dictionary<string, object>();

            // Store minutes-since-midnight for start/end
            var startMinutes = Start.Hour * 60 + Start.Minute;
            var endMinutes = End.Hour * 60 + End.Minute;

            if (startMinutes != 0 || endMinutes != 0)
            {
                dict["start"] = (long)startMinutes;
                dict["end"] = (long)endMinutes;
            }

            if (Invert)
                dict["inv"] = true;

            return dict;
        }
    }

    public unsafe bool Evaluate(WorldState world, IReadOnlyDictionary<string, object> parameters)
    {
        var args = GetParams(parameters);

        // If no window configured, treat as always-true (unless inverted).
        if (args.Start == default && args.End == default)
            return !args.Invert;

        var clientTime = Framework.Instance()->ClientTime.EorzeaTime;
        var eorzeaTime = TimeOnly.FromDateTime(DateTimeOffset.FromUnixTimeSeconds(clientTime).DateTime);

        // Use same inclusive semantics as the existing time-window helper:
        // - If start <= end: simple range
        // - If start > end: window wraps over midnight
        bool inWindow = args.Start <= args.End
            ? eorzeaTime >= args.Start && eorzeaTime <= args.End
            : eorzeaTime >= args.Start || eorzeaTime <= args.End;

        var result = inWindow;
        return args.Invert ? !result : result;
    }

    public void DrawParams(Condition condition)
    {
        var args = GetParams(condition.Params);
        var start = args.Start;
        var end = args.End;

        var startText = start.ToString("HH:mm");
        ImGui.SetNextItemWidth(80 * ImGuiHelpers.GlobalScale);
        if (ImGui.InputText("Start (HH:mm)", ref startText, 5))
        {
            if (TimeOnly.TryParse(startText, out var parsed))
            {
                args = args with { Start = parsed };
                condition.Params = args.ToParams();
            }
        }

        ImGui.SameLine();

        var endText = end.ToString("HH:mm");
        ImGui.SetNextItemWidth(80 * ImGuiHelpers.GlobalScale);
        if (ImGui.InputText("End (HH:mm)", ref endText, 5))
        {
            if (TimeOnly.TryParse(endText, out var parsed))
            {
                args = args with { End = parsed };
                condition.Params = args.ToParams();
            }
        }
    }

    private static TimeWindowParams GetParams(IReadOnlyDictionary<string, object> p)
    {
        var (start, end) = GetTimeWindowFromParams(p);
        var invert = IConditionDefinition.GetBool(p, "inv", false);
        return new TimeWindowParams(start, end, invert);
    }

    public static (TimeOnly Start, TimeOnly End) GetTimeWindowFromParams(IReadOnlyDictionary<string, object> p)
    {
        var startMinutes = IConditionDefinition.GetInt(p, "start", 0);
        var endMinutes = IConditionDefinition.GetInt(p, "end", 0);
        startMinutes = Math.Clamp(startMinutes, 0, 24 * 60 - 1);
        endMinutes = Math.Clamp(endMinutes, 0, 24 * 60 - 1);
        var start = new TimeOnly(startMinutes / 60, startMinutes % 60);
        var end = new TimeOnly(endMinutes / 60, endMinutes % 60);
        return (start, end);
    }

    (bool Enabled, TimeOnly Start, TimeOnly End) ISimpleConditionValue<(bool Enabled, TimeOnly Start, TimeOnly End)>.FromParams(IReadOnlyDictionary<string, object> p)
    {
        var (start, end) = GetTimeWindowFromParams(p);
        return (true, start, end);
    }

    IReadOnlyDictionary<string, object>? ISimpleConditionValue<(bool Enabled, TimeOnly Start, TimeOnly End)>.ToParams((bool Enabled, TimeOnly Start, TimeOnly End) value, object? context)
        => value.Enabled ? new TimeWindowParams(value.Start, value.End, false).ToParams() : null;
}

