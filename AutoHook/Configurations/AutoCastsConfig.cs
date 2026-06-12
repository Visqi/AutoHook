using AutoHook.Conditions;
using AutoHook.Conditions.Definitions;
using Newtonsoft.Json;

namespace AutoHook.Configurations;

public class AutoCastsConfig {
    public bool EnableAll = false;

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
        var output = new List<BaseActionCast>
        {
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

    public BaseActionCast? GetNextAutoCast(bool ignoreCurrentMooch) {
        if (!EnableAll)
            return null;

        BaseActionCast? cast = null;

        var order = GetAutoCastOrder();

        foreach (var action in order.Where(action => action.IsAvailableToCast(ignoreCurrentMooch))) {
            if (action.RequiresTimeWindow() && !TimeWindow.BackingSet.PassesOrUnconfigured())
                continue;

            Service.PrintDebug($"[AutoCast] Returning {action.GetName()}");
            return action;
        }

        return cast;
    }

    [JsonProperty("TimeWindowConditionSet")]
    [JsonConverter(typeof(SingleConditionConverter))]
    public SingleCondition<TimeWindowCD, (bool Enabled, TimeOnly Start, TimeOnly End)> TimeWindow { get; set; } = new SingleCondition<TimeWindowCD, (bool Enabled, TimeOnly Start, TimeOnly End)>();

    public bool TryCastAction(BaseActionCast? action, bool noDelay = false, bool ignoreCurrentMooch = false) {
        if (action == null || !EnableAll)
            return false;

        if (action.RequiresTimeWindow() && !TimeWindow.BackingSet.PassesOrUnconfigured())
            return false;

        if (!action.Enabled || !action.IsAvailableToCast(ignoreCurrentMooch))
            return false;

        if (action.Id == IDs.Actions.Chum && ChumAnimationCancel) {
            TryChumAnimationCancel();
            return true;
        }

        if (noDelay)
            PlayerRes.CastActionNoDelay(action.Id, action.ActionType, action.GetName());
        else
            PlayerRes.CastActionDelayed(action.Id, action.ActionType, action.GetName());

        return true;
    }

    private void TryChumAnimationCancel() {
        Service.PrintDebug("Trying to cancel chum animation");
        // Make sure Salvage is disabled before chum

        Service.TaskManager.EnqueueDelay(40);
        Service.TaskManager.Enqueue(() => PlayerRes.CastAction(IDs.Actions.Chum));

        // Recast Salvage a few ms's later, maybe 500 is enough?
        Service.TaskManager.EnqueueDelay(465);
        Service.TaskManager.Enqueue(() => PlayerRes.CastAction(IDs.Actions.Salvage));
    }
}
