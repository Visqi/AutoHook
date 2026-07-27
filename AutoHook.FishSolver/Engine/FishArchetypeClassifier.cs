using AutoHook.FishSolver.Models;

namespace AutoHook.FishSolver.Engine;

// pick the archeetype for a fish + how to hold until the window
//
// intuition:
// - few triggers (1-3) -> zero-time: last trigger at window open
// - many triggers (4+) -> rebuild: farm N-1, IC the last at 0s, release at open
//
// mooch (no intuition):
// - if matching lure makes the target (nearly) exclusive on a straight bait, prefer that over mooch (e.g. thunderbolt eel)
// - Spareful Hand -> bank swimbait; else hold a live mooch (don't stow)
// - multi-step chain -> same, longer fragile hold
//
// straight catch:
// - short bite vs long pool mates -> Rest / lure early (ShortBiteReset)
// - long !!! with a slower competitor -> slap the slow one (FailFaster)
// - minority hookset + matching lure -> LureStack (guide)
// - else slap junk + chum (SlapAndChum)
public static class FishArchetypeClassifier {
    public static InferredTactics Classify(FishProfile profile, PlayerProfile player) {
        var pool = profile.PoolAtPrimarySpot;
        var archetype = InferArchetype(profile, pool, player);
        var holdMode = PrepHoldModeSelector.Select(profile, player, archetype);

        // LureStack prefers slapping the same-hookset competitor; otherwise default slap logic
        int? slapTarget = null;
        if (player.Skills.SurfaceSlap) {
            slapTarget = archetype == StrategyArchetype.LureStack ? SpotBaitPoolAnalyzer.RecommendLureStackSlapTarget(profile, pool) : SpotBaitPoolAnalyzer.RecommendSlapTarget(profile, pool);
        }

        var earlyCancel = SpotBaitPoolAnalyzer.ComputeEarlyCancelSec(profile, pool);
        var hookOnly = SpotBaitPoolAnalyzer.HookOnlyTugs(profile, pool);

        // most holds die on stow; CrossWindowJail is the exception
        var stowSafe = holdMode is PrepHoldMode.CrossWindowJail or PrepHoldMode.None && profile.Acquisition.Predators.Count > 0;
        return new InferredTactics {
            Archetype = archetype,
            HoldMode = holdMode,
            SlapTargetFishId = slapTarget,
            HookOnlyTugs = hookOnly,
            EarlyCancelSec = earlyCancel,
            RequiresContinuousFishing = PrepHoldModeSelector.RequiresContinuousFishing(holdMode),
            StowRodSafeDuringHold = stowSafe || holdMode == PrepHoldMode.CrossWindowJail,
        };
    }

    private static StrategyArchetype InferArchetype(FishProfile profile, IReadOnlyList<PoolMember> pool, PlayerProfile player) {
        // --- intuition ---
        // Predator list comes from Teamcraft. Quantity sum is a rough proxy for "how annoying is the rebuild".
        // >3 total fish-to-catch usually wants IC zero-time prep (Sea Butterfly 3+3, Problematicus 3+5, etc.).
        if (profile.Acquisition.Predators.Count > 0) {
            if (profile.Acquisition.Predators.Sum(p => p.Quantity) > 3)
                return StrategyArchetype.IntuitionRebuild;
            return StrategyArchetype.IntuitionZeroTime;
        }

        // --- mooch without intuition ---
        // Multi-step chains stay mooch. Single mooch can lose to lure-isolated straight catch (e.g. Thunderbolt Eel)
        if (profile.Acquisition.MoochChain.Count > 1)
            return StrategyArchetype.MoochChain;

        if (profile.Acquisition.MoochChain.Count == 1 && !PreferLureStraightOverSingleMooch(profile, pool, player))
            return player.Skills.SparefulHand ? StrategyArchetype.SwimbaitBank : StrategyArchetype.PreMoochOpener;

        // --- straight catch legendaries / grinders ---
        // Target bites early, pool mates bite late -> Rest (or lure) instead of waiting out a dead cast.
        if (SpotBaitPoolAnalyzer.ComputeEarlyCancelSec(profile, pool) is > 0 and <= 12)
            return StrategyArchetype.ShortBiteReset;

        // Long target + even longer competitor -> slap the slow one so its bite can't waste the cast.
        if (SpotBaitPoolAnalyzer.RecommendSlapTarget(profile, pool) is { } slap) {
            var competitor = pool.FirstOrDefault(p => p.FishId == slap);
            if (competitor != null && competitor.BiteMax > profile.Signals.BiteTimeMin) {
                var targetDuration = profile.Signals.BiteTimeMax - profile.Signals.BiteTimeMin;
                if (profile.Signals.Tug == TugType.Legendary && targetDuration >= 10)
                    return StrategyArchetype.FailFaster;
            }
        }

        // guide: minority hookset + ≤2 same-hookset siblings + matching lure skill
        // also: lure-eligible fish where ALure/MLure isolates the target (solo or ≤1 sibling)
        if (HasMatchingLure(profile, player)
            && (SpotBaitPoolAnalyzer.IsLureStackCandidate(profile, pool)
                || SpotBaitPoolAnalyzer.IsLureNearlyExclusive(profile, pool)))
            return StrategyArchetype.LureStack;

        return StrategyArchetype.SlapAndChum;
    }

    // Single-mooch fish that respond to A/MLure as a straight catch is preferred over mooch even when mooch has a higher raw bite rate
    private static bool PreferLureStraightOverSingleMooch(FishProfile profile, IReadOnlyList<PoolMember> pool, PlayerProfile player) {
        if (!HasMatchingLure(profile, player))
            return false;
        if (!profile.Eligibility.ALureEligible && !profile.Eligibility.MLureEligible)
            return false;
        // only when primary bait is already a tackle bait (not the mooch fish itself)
        if (profile.Acquisition.MoochChain.Contains(profile.Eligibility.BaitId))
            return false;
        return SpotBaitPoolAnalyzer.IsLureNearlyExclusive(profile, pool) || SpotBaitPoolAnalyzer.IsLureStackCandidate(profile, pool);
    }

    private static bool HasMatchingLure(FishProfile profile, PlayerProfile player)
        => profile.Signals.Hookset switch {
            HooksetType.Powerful => player.Skills.AmbitiousLure,
            HooksetType.Precision => player.Skills.ModestLure,
            _ => false,
        };
}

// what we're sitting on until the window opens
// - IdenticalCastZeroTime: farm N-1, IC last trigger at 0s (dies on stow)
// - CrossWindowJail: no IC - farm across windows, stow ok
// - Immediate: single trigger, catch when window is up
// - SwimbaitBank / MoochHold: bank with Spareful Hand, else hold live mooch
public static class PrepHoldModeSelector {
    public static PrepHoldMode Select(FishProfile profile, PlayerProfile player, StrategyArchetype archetype) {
        if (profile.Acquisition.Predators.Count > 0) {
            var total = profile.Acquisition.Predators.Sum(p => p.Quantity);
            if (total > 1 && player.Skills.IdenticalCast)
                return PrepHoldMode.IdenticalCastZeroTime;
            if (total > 1)
                return PrepHoldMode.CrossWindowJail;
            return PrepHoldMode.Immediate;
        }

        if (archetype == StrategyArchetype.SwimbaitBank && player.Skills.SparefulHand)
            return PrepHoldMode.SwimbaitBank;

        // Lure / straight plans skip mooch hold even when a mooch alternate exists
        if (archetype is StrategyArchetype.LureStack or StrategyArchetype.LureReroll or StrategyArchetype.SlapAndChum or StrategyArchetype.FailFaster or StrategyArchetype.ShortBiteReset)
            return PrepHoldMode.None;

        if (profile.Acquisition.MoochChain.Count > 0)
            return PrepHoldMode.MoochHold;

        return PrepHoldMode.None;
    }

    // If true, putting the rod away mid-prep ruins the plan (lose IC / mooch / swimbait).
    public static bool RequiresContinuousFishing(PrepHoldMode mode) => mode switch {
        PrepHoldMode.IdenticalCastZeroTime => true,
        PrepHoldMode.MoochHold => true,
        PrepHoldMode.SwimbaitBank => true,
        PrepHoldMode.CollectableDialogHold => true,
        PrepHoldMode.TimedLastCatch => true,
        PrepHoldMode.CrossWindowJail => false,
        _ => false,
    };
}

