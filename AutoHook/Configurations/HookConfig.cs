using AutoHook.Conditions;
using AutoHook.Conditions.Definitions;
using FFXIVClientStructs.FFXIV.Client.Game;
using Newtonsoft.Json;

namespace AutoHook.Configurations;

public class SwimbaitConfig {
    public bool UseSwimbait = false;
    public ConditionSet? ConditionSet { get; set; }
}

public class HookConfig : BaseOption {
    public bool Enabled = true;

    public BaitFishClass BaitFish = new();

    public BaseHookset NormalHook = new(IDs.Status.None);
    public BaseHookset IntuitionHook = new(IDs.Status.FishersIntuition);

    public SwimbaitConfig SwimbaitNormal { get; set; } = new();
    public SwimbaitConfig SwimbaitIntuition { get; set; } = new();
    public NotificationConfig NotifyOnSuccess { get; set; } = new();

    public bool StopAfterResetCount;
    public FishingSteps StopFishingStep = FishingSteps.None;

    [JsonProperty("StopConditionSet")]
    [JsonConverter(typeof(SingleConditionConverter))]
    public SingleCondition<HookCountCD, (bool Enabled, int Limit)> StopAfterCaughtLimit { get => field ??= new SingleCondition<HookCountCD, (bool Enabled, int Limit)>(() => UniqueId); set; }

    //todo enable more hook settings based on the current status
    //List<BaseHookset> CustomHooksets = new();

    public HookConfig() { }

    public HookConfig(BaitFishClass baitFish) {
        BaitFish = baitFish;
    }

    public HookConfig(int baitFishId) {
        BaitFish = new BaitFishClass(baitFishId);
    }

    public void SetBiteAndHookType(BiteType bite, HookType hookType, bool isIntuition = false) {
        var hookset = isIntuition ? IntuitionHook : NormalHook;
        var hookDictionary = new Dictionary<BiteType, (BaseBiteConfig th, BaseBiteConfig dh, BaseBiteConfig ph)>
        {
            { BiteType.Weak, (hookset.TripleWeak, hookset.DoubleWeak, hookset.PatienceWeak) },
            { BiteType.Strong, (hookset.TripleStrong, hookset.DoubleStrong, hookset.PatienceStrong) },
            { BiteType.Legendary, (hookset.TripleLegendary, hookset.DoubleLegendary, hookset.PatienceLegendary) }
        };

        if (hookDictionary.TryGetValue(bite, out var hook)) {
            hook.ph.HooksetEnabled = true;
            hook.ph.HooksetType = hookType;

            hook.dh.HooksetEnabled = true;
            hook.th.HooksetEnabled = true;
        }
    }

    public void SetHooksetTimer(BiteType bite, double min, double max, bool isIntuition = false) {
        var hookset = isIntuition ? IntuitionHook : NormalHook;
        var hookDictionary = new Dictionary<BiteType, (BaseBiteConfig th, BaseBiteConfig dh, BaseBiteConfig ph)>
        {
            { BiteType.Weak, (hookset.TripleWeak, hookset.DoubleWeak, hookset.PatienceWeak) },
            { BiteType.Strong, (hookset.TripleStrong, hookset.DoubleStrong, hookset.PatienceStrong) },
            { BiteType.Legendary, (hookset.TripleLegendary, hookset.DoubleLegendary, hookset.PatienceLegendary) }
        };

        if (!hookDictionary.TryGetValue(bite, out var hook)) return;

        var maxSec = max + 1;
        var biteTimerId = ConditionRegistry.Registry.GetId<BiteTimerCD>();
        foreach (var biteCfg in new[] { hook.ph, hook.dh, hook.th })
            SetBiteTimerInConditionSet(biteCfg, biteTimerId, min, maxSec);
    }

    private static void SetBiteTimerInConditionSet(BaseBiteConfig biteCfg, string biteTimerId, double min, double max) {
        var set = biteCfg.ConditionSet ??= new ConditionSet();
        Condition? found = null;
        foreach (var group in set.Groups) {
            found = group.Conditions.FirstOrDefault(c => c.TypeId == biteTimerId);
            if (found != null) break;
        }
        if (found != null) {
            found.Params["r"] = new List<object> { min, max };
            return;
        }
        var newGroup = new ConditionGroup { CombineMode = ConditionCombineMode.Any };
        newGroup.Conditions.Add(new Condition {
            TypeId = biteTimerId,
            Params = new Dictionary<string, object> { ["r"] = new List<object> { min, max } }
        });
        set.Groups.Add(newGroup);
    }

    public void ResetAllHooksets() {
        ResetHooksets(NormalHook);
        ResetHooksets(IntuitionHook);
    }

    private void ResetHooksets(BaseHookset hookset) {
        var hookDictionary = new Dictionary<BiteType, (BaseBiteConfig th, BaseBiteConfig dh, BaseBiteConfig ph)>
        {
            { BiteType.Weak, (hookset.TripleWeak, hookset.DoubleWeak, hookset.PatienceWeak) },
            { BiteType.Strong, (hookset.TripleStrong, hookset.DoubleStrong, hookset.PatienceStrong) },
            { BiteType.Legendary, (hookset.TripleLegendary, hookset.DoubleLegendary, hookset.PatienceLegendary) }
        };

        foreach (var hookDisable in hookDictionary) {
            hookDisable.Value.ph.HooksetEnabled = false;
            hookDisable.Value.dh.HooksetEnabled = false;
            hookDisable.Value.th.HooksetEnabled = false;
        }
    }

    public BaseHookset GetHookset() {
        return UsesIntuitionHookConfig() ? IntuitionHook : NormalHook;
    }

    /// <summary>
    /// Same intuition vs normal window as <see cref="GetHookset"/> — used for swimbait selection.
    /// </summary>
    public SwimbaitConfig GetSwimbaitConfig()
        => UsesIntuitionHookConfig() ? SwimbaitIntuition : SwimbaitNormal;

    public bool UsesIntuitionHookConfig()
        => Service.WorldState.Fishing.Intuition.Status == IntuitionStatus.Active && IntuitionHook.UseCustomStatusHook;

    public HookType? GetHook(BiteType bite, double timePassed) {
        var hookset = GetHookset();

        var hookDictionary = new Dictionary<BiteType, (BaseBiteConfig th, BaseBiteConfig dh, BaseBiteConfig ph)>
        {
            { BiteType.Weak, (hookset.TripleWeak, hookset.DoubleWeak, hookset.PatienceWeak) },
            { BiteType.Strong, (hookset.TripleStrong, hookset.DoubleStrong, hookset.PatienceStrong) },
            { BiteType.Legendary, (hookset.TripleLegendary, hookset.DoubleLegendary, hookset.PatienceLegendary) }
        };

        Service.Status = "";

        if (hookDictionary.TryGetValue(bite, out var hook)) {
            // Triple Hook
            if (hookset.UseTripleHook && hook.th.HooksetEnabled) {
                if (CheckHookCondition(hook.th, timePassed))
                    if (GetHookTypeForTime(hook.th, timePassed) is { } ht && IsHookAvailable(hook.th, timePassed))
                        return ht;

                if (hookset.LetFishEscapeTripleHook && Service.WorldState.Player.CurrentGp < 700) {
                    Service.Status = "Not enough GP to use Triple Hook, Letting fish escape is enabled";
                    return HookType.None;
                }

                Service.Status = $"(Triple Hook) {Service.Status}";
            }

            // Double Hook
            if (hookset.UseDoubleHook && hook.dh.HooksetEnabled) {
                if (CheckHookCondition(hook.dh, timePassed))
                    if (GetHookTypeForTime(hook.dh, timePassed) is { } ht && IsHookAvailable(hook.dh, timePassed))
                        return ht;

                if (hookset.LetFishEscapeDoubleHook && Service.WorldState.Player.CurrentGp < 400) {
                    Service.Status = "Not enough GP to use Double Hook, Letting fish escape is enabled";
                    return HookType.None;
                }

                Service.Status = $"(Triple Hook) {Service.Status}";
            }

            // Normal - Patience
            if (hook.ph.HooksetEnabled) {
                if (CheckHookCondition(hook.ph, timePassed)) {
                    if (GetHookTypeForTime(hook.ph, timePassed) is { } ht) {
                        if (IsHookAvailable(hook.ph, timePassed)) return ht;
                        var fallback = ht switch {
                            HookType.Stellar when bite is BiteType.Weak => HookType.Precision,
                            HookType.Stellar when bite is not BiteType.Weak => HookType.Powerful,
                            _ => HookType.Normal
                        };
                        return fallback != ht && Service.WorldState.ActionAvailable((uint)fallback, ActionType.Action) ? fallback : HookType.Normal;
                    }
                    Service.Status = "(Normal/Patience Hook) No hook type for current bite timer.";
                }
                else
                    Service.Status = $"(Normal/Patience Hook) {Service.Status}";
            }
            else if (Service.Status == "")
                Service.Status = UIStrings.Status_NoHookEnabled;
        }

        //Service.Status = "Skipping bite - No hook for this bite is enabled";
        return HookType.None;
    }

    private bool CheckHookCondition(BaseBiteConfig hookType, double timePassed)
        => hookType.ConditionSet is not { Groups.Count: > 0 } || hookType.ConditionSet.Evaluate(Service.WorldState, ConditionRegistry.Registry);

    private HookType? GetHookTypeForTime(BaseBiteConfig hookType, double timePassed)
        => hookType.UseMultipleHookTypesByTimer
            ? GetTimedHookType(hookType, timePassed) is { } timedHook ? timedHook : null
            : HookTypeConditionsPass(hookType.HookTypeConditionSet)
                ? hookType.HooksetType
                : null;

    private HookType? GetTimedHookType(BaseBiteConfig hookType, double timePassed) {
        bool InRange(bool enabled, double min, double max) {
            if (!enabled)
                return false;

            if (min > 0 && timePassed < min)
                return false;

            return max <= 0 || timePassed <= max;
        }

        // Highest value hook types first if multiple windows overlap
        if (InRange(hookType.UseStellarHookTypeByTimer, hookType.StellarHookTypeMin, hookType.StellarHookTypeMax)
            && HookTypeConditionsPass(hookType.StellarHookTypeConditionSet))
            return HookType.Stellar;

        if (InRange(hookType.UsePowerfulHookTypeByTimer, hookType.PowerfulHookTypeMin, hookType.PowerfulHookTypeMax)
            && HookTypeConditionsPass(hookType.PowerfulHookTypeConditionSet))
            return HookType.Powerful;

        if (InRange(hookType.UsePrecisionHookTypeByTimer, hookType.PrecisionHookTypeMin, hookType.PrecisionHookTypeMax)
            && HookTypeConditionsPass(hookType.PrecisionHookTypeConditionSet))
            return HookType.Precision;

        if (InRange(hookType.UseNormalHookTypeByTimer, hookType.NormalHookTypeMin, hookType.NormalHookTypeMax)
            && HookTypeConditionsPass(hookType.NormalHookTypeConditionSet))
            return HookType.Normal;

        return null;
    }

    private static bool HookTypeConditionsPass(ConditionSet? set)
        => set is not { Groups.Count: > 0 } || set.Evaluate(Service.WorldState, ConditionRegistry.Registry);

    private bool IsHookAvailable(BaseBiteConfig hookType, double timePassed) {
        if (GetHookTypeForTime(hookType, timePassed) is not { } timedHook)
            return false;
        if (!Service.WorldState.ActionAvailable((uint)timedHook, ActionType.Action)) {
            Service.Status = UIStrings.Status_HookNotAvailableNormalWillBeUsed;
            return false;
        }

        return true;
    }

    public override void DrawOptions() { }

    public override bool Equals(object? obj) => obj is HookConfig settings && BaitFish == settings.BaitFish;
    public override int GetHashCode() => HashCode.Combine(UniqueId);
}
