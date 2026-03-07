using AutoHook.Conditions;
using AutoHook.Conditions.Definitions;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using System.ComponentModel;

namespace AutoHook.Configurations;

public class AutoCastsConfig
{
    public bool EnableAll = false;

    [DefaultValue(true)] public bool DontCancelMooch = true;

    public TimeOnly StartTime = new(0);
    public TimeOnly EndTime = new(0);

    public bool OnlyCastDuringSpecificTime = false;

    /// <summary>
    /// Optional global auto-cast time window expressed as a ConditionSet.
    /// Typically a single <see cref="TimeWindowCD"/> condition.
    /// </summary>
    public ConditionSet? TimeWindowConditionSet { get; set; }

    public bool RecastAnimationCancel;
    public bool TurnCollectOff;
    public bool ChumAnimationCancel;
    public bool TurnCollectOffWithoutAnimCancel;

    public AutoCastLine CastLine = new();
    public AutoMooch CastMooch = new();
    public AutoChum CastChum = new();
    public AutoCollect CastCollect = new();
    public AutoCordial CastCordial = new();
    public AutoFishEyes CastFishEyes = new();
    public AutoMakeShiftBait CastMakeShiftBait = new();
    public AutoPatience CastPatience = new();
    public AutoPrizeCatch CastPrizeCatch = new();
    public AutoThaliaksFavor CastThaliaksFavor = new();
    public AutoBigGameFishing CastBigGame = new();
    public AutoSurfaceSlap CastSurfaceSlap = new();
    public AutoMultiHook CastMultihook = new();
    public AutoIdenticalCast CastIdenticalCast = new();
    //public AutoLures CastLures = new();

    private List<BaseActionCast> GetAutoCastOrder()
    {
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
            CastBigGame,
            CastMultihook,
        }.OrderBy(x => x.Priority).ToList();

        return output;
    }

    public BaseActionCast? GetNextAutoCast(bool ignoreCurrentMooch)
    {
        if (!EnableAll)
            return null;

        BaseActionCast? cast = null;

        var order = GetAutoCastOrder();

        foreach (var action in order.Where(action => action.IsAvailableToCast(ignoreCurrentMooch)))
        {
            if (action.RequiresTimeWindow() && !IsWithinTimeWindow())
                continue;

            Service.PrintDebug($"[AutoCast] Returning {action.Name}");
            return action;
        }

        return cast;
    }

    /// <summary>
    /// Returns true if the current time is within the configured time window, using
    /// a ConditionSet when available and falling back to the legacy check otherwise.
    /// </summary>
    private bool IsWithinTimeWindow()
    {
        if (!OnlyCastDuringSpecificTime)
            return true;

        if (TimeWindowConditionSet is { Groups.Count: > 0 } set)
            return set.Evaluate(Service.WorldState, ConditionRegistry.Registry);

        return InsideCastWindow();
    }

    private unsafe bool InsideCastWindow()
    {
        var clientTime = Framework.Instance()->ClientTime.EorzeaTime;
        var eorzeaTime = TimeOnly.FromDateTime(DateTimeOffset.FromUnixTimeSeconds(clientTime).DateTime);

        return eorzeaTime.IsBetween(StartTime, EndTime);
    }

    public bool TryCastAction(BaseActionCast? action, bool noDelay = false, bool ignoreCurrentMooch = false)
    {
        if (action == null || !EnableAll)
            return false;

        if (action.RequiresTimeWindow() && !IsWithinTimeWindow())
            return false;

        if (!action.Enabled || !action.IsAvailableToCast(ignoreCurrentMooch))
            return false;

        if (action.Id == IDs.Actions.Chum && ChumAnimationCancel)
            TryChumAnimationCancel();
        else if (noDelay)
            PlayerRes.CastActionNoDelay(action.Id, action.ActionType, action.GetName());
        else
            PlayerRes.CastActionDelayed(action.Id, action.ActionType, action.GetName());

        return true;
    }

    /// <summary>
    /// Keep <see cref="TimeWindowConditionSet"/> in sync with <see cref="StartTime"/>,
    /// <see cref="EndTime"/> and <see cref="OnlyCastDuringSpecificTime"/>.
    /// </summary>
    public void SyncTimeWindowCondition()
    {
        if (!OnlyCastDuringSpecificTime)
            return;

        var set = TimeWindowConditionSet ??= new ConditionSet
        {
            CombineMode = ConditionCombineMode.All,
        };

        ConditionGroup group;
        if (set.Groups.Count > 0)
        {
            group = set.Groups[0];
        }
        else
        {
            group = new ConditionGroup { CombineMode = ConditionCombineMode.All };
            set.Groups.Add(group);
        }

        var typeId = ConditionRegistry.Registry.GetId<TimeWindowCD>();
        var cond = group.Conditions.FirstOrDefault(c => c.TypeId == typeId);
        if (cond == null)
        {
            cond = new Condition
            {
                TypeId = typeId,
                Params = new TimeWindowCD.TimeWindowParams(StartTime, EndTime, false).ToParams(),
            };
            group.Conditions.Add(cond);
        }
        else
        {
            var args = new TimeWindowCD.TimeWindowParams(StartTime, EndTime, false);
            cond.Params = args.ToParams();
        }
    }

    private void TryChumAnimationCancel()
    {
        Service.PrintDebug("Trying to cancel chum animation");
        // Make sure Salvage is disabled before chum

        Service.TaskManager.EnqueueDelay(40);
        Service.TaskManager.Enqueue(() => PlayerRes.CastAction(IDs.Actions.Chum));

        // Recast Salvage a few ms's later, maybe 500 is enough?
        Service.TaskManager.EnqueueDelay(465);
        Service.TaskManager.Enqueue(() => PlayerRes.CastAction(IDs.Actions.Salvage));
    }
}
