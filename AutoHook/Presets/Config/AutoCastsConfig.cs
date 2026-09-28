using AutoHook.Conditions;
using AutoHook.Conditions.Definitions;
using Newtonsoft.Json;
using System.ComponentModel;

namespace AutoHook.Presets.Config;

public class AutoCastsConfig {
    public bool EnableAll = false;

    [DefaultValue(true)]
    public bool DontCancelMooch = true;

    public bool RecastAnimationCancel;
    public bool TurnCollectOff;
    public bool ChumAnimationCancel;
    public bool TurnCollectOffWithoutAnimCancel;

    public AutoCastLine CastLine = new();
    public AutoMooch CastMooch = new();
    public AutoChum CastChum = new();
    public AutoCollect CastCollect = new();
    public AutoSnagging CastSnagging = new();
    public AutoCordial CastCordial = new();
    public AutoFishEyes CastFishEyes = new();
    public AutoMakeShiftBait CastMakeShiftBait = new();
    public AutoPatience CastPatience = new();
    public AutoPrizeCatch CastPrizeCatch = new();
    public AutoThaliaksFavor CastThaliaksFavor = new();
    public AutoBigGameFishing CastBigGame = new();
    public AutoMultiHook CastMultihook = new();

    private List<BaseActionCast> GetAutoCastOrder() {
        var output = new List<BaseActionCast> {
            CastThaliaksFavor,
            CastCordial,
            CastPatience,
            CastMakeShiftBait,
            CastChum,
            CastFishEyes,
            CastPrizeCatch,
            //CastCollect,
            CastSnagging,
            CastBigGame,
            CastMultihook,
        }.OrderBy(x => x.Priority).ToList();

        return output;
    }

    public BaseActionCast? GetNextAutoCast(WorldState ws, bool ignoreCurrentMooch)
        => GetNextAutoCast(ws, GetAutoCastOrder(), ignoreCurrentMooch);

    public BaseActionCast? GetNextGpRestoringCast(WorldState ws, bool ignoreCurrentMooch)
        => GetNextAutoCast(ws, GetAutoCastOrder().Where(action => action.RestoresGp), ignoreCurrentMooch);

    private BaseActionCast? GetNextAutoCast(WorldState ws, IEnumerable<BaseActionCast> order, bool ignoreCurrentMooch) {
        if (!EnableAll)
            return null;

        foreach (var action in order.Where(action => action.IsAvailableToCast(ws, ignoreCurrentMooch))) {
            if (action.RequiresTimeWindow() && !TimeWindow.BackingSet.PassesOrUnconfigured(ws)) {
                LogAutoCastDecision(ws, action, "Time window blocked");
                continue;
            }

            return action;
        }

        return null;
    }

    [JsonProperty("TimeWindowConditionSet")]
    [JsonConverter(typeof(SingleConditionConverter))]
    public SingleCondition<TimeWindowCD, (bool Enabled, TimeOnly Start, TimeOnly End)> TimeWindow { get; set; } = new SingleCondition<TimeWindowCD, (bool Enabled, TimeOnly Start, TimeOnly End)>();

    public void LogAutoCastDecision(WorldState ws, BaseActionCast action, string? failureReason = null) {
        // this is only for logging failures
        // success Decide in ActionHintsResolve
        if (failureReason == null)
            return;

        var detail = failureReason;
        var cond = action.ConditionSet?.Describe() ?? "";
        if (action.RequiresTimeWindow() && TimeWindow.BackingSet is { } timeWindow) {
            var global = timeWindow.Describe();
            if (!string.IsNullOrEmpty(global))
                cond = string.IsNullOrEmpty(cond) ? $"Global:\n{global}" : $"{cond}\nGlobal:\n{global}";
        }

        if (!string.IsNullOrEmpty(cond))
            detail = string.IsNullOrEmpty(detail) ? cond : $"{detail}\n{cond}";

        ws.Decide(DecisionContext.AutoCast, false, action.GetName(), string.IsNullOrEmpty(detail) ? null : detail);
    }
}
