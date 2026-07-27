using AutoHook.FishSolver.Models;

namespace AutoHook.FishSolver.Engine;

// reading a (spot, bait) pool:
// 1. slap target: common junk, or the slow fish that steals casts from a long !!! (fail-faster)
// 2. early cancel: if every other !!! bites before ours can start, Rest after that; same for short-bite targets (≤12s)
// 3. which tugs to hook: usually only the target's (intuition / sparse !!!) - hooking junk burns GP/slap/IC
// rate tiers are simple relative odds when there's no histogram
public static class SpotBaitPoolAnalyzer {
    public static int? RecommendSlapTarget(FishProfile target, IReadOnlyList<PoolMember> pool) {
        // Solo pool - nothing to slap.
        if (pool.Count <= 1)
            return null;

        var competitors = pool.Where(p => p.FishId != target.FishId).ToList();
        if (competitors.Count == 0)
            return null;

        // Fail-faster branch: long !!! target whose cast is often eaten by something that bites even later
        // Slap the slow fish so the cast either becomes the target or ends sooner
        var targetDuration = target.Signals.BiteTimeMax - target.Signals.BiteTimeMin;
        var targetIsLong = target.Signals.Tug == TugType.Legendary && targetDuration >= 10;

        if (targetIsLong) {
            var slowest = competitors.OrderByDescending(c => c.BiteMax - c.BiteMin).ThenByDescending(c => c.BiteMax).First();
            // Only worth it if the slow fish can still bite after our target starts being possible
            if (slowest.BiteMax >= target.Signals.BiteTimeMin)
                return slowest.FishId;
        }

        // Default: slap the most common leftover. Prefer common rate tier, then later biters as a weak tie-break
        // late common junk on long casts suck
        return MostCommonCompetitor(competitors);
    }

    // Lure Stack: slap another fish with the same hookset when present, else most common junk
    public static int? RecommendLureStackSlapTarget(FishProfile target, IReadOnlyList<PoolMember> pool) {
        var hookset = target.Signals.Hookset;
        if (hookset is not (HooksetType.Precision or HooksetType.Powerful))
            return RecommendSlapTarget(target, pool);

        var sameHook = pool
            .Where(p => p.FishId != target.FishId && p.Hookset == hookset)
            .ToList();
        if (sameHook.Count > 0)
            return MostCommonCompetitor(sameHook);

        return RecommendSlapTarget(target, pool);
    }

    // Lure Stack when target hookset is minority by fish count, or by rate weight when counts tie
    // also wants ≤2 other fish sharing that hookset so the lure bias isn't diluted
    public static bool IsLureStackCandidate(FishProfile target, IReadOnlyList<PoolMember> pool) {
        var hookset = target.Signals.Hookset;
        if (hookset is not (HooksetType.Precision or HooksetType.Powerful))
            return false;

        var opposite = hookset == HooksetType.Powerful ? HooksetType.Precision : HooksetType.Powerful;
        var same = pool.Where(p => p.Hookset == hookset).ToList();
        var opp = pool.Where(p => p.Hookset == opposite).ToList();
        if (same.Count == 0 || opp.Count == 0)
            return false; // all one hookset → lure useless

        // ideally 0–2 others with the same hookset (target alone, or 1–2 siblings)
        if (same.Count > 3)
            return false;

        if (same.Count < opp.Count)
            return true;

        if (same.Count > opp.Count)
            return false;

        // equal counts → minority by summed bite-rate weight
        return RateWeightSum(same) < RateWeightSum(opp);
    }

    private static int MostCommonCompetitor(IReadOnlyList<PoolMember> competitors)
        => competitors
            .OrderBy(c => c.RateTier switch {
                RateTier.Common => 0,
                RateTier.Uncommon => 1,
                RateTier.Rare => 2,
                RateTier.VeryRare => 3,
                _ => 4,
            })
            .ThenByDescending(c => c.BiteMax)
            .First().FishId;

    private static double RateWeightSum(IEnumerable<PoolMember> members)
        => members.Sum(m => TierWeight(m.RateTier));

    public static double? ComputeEarlyCancelSec(FishProfile target, IReadOnlyList<PoolMember> pool) {
        // Early cancel is a !!! trick - weak/strong bites don't get the same clean separation
        if (target.Signals.Tug != TugType.Legendary)
            return null;

        var sameTug = pool.Where(p => p.Tug == target.Signals.Tug && p.FishId != target.FishId).ToList();
        if (sameTug.Count == 0)
            return null;

        // Clean separation: every other !!! finishes biting before ours can start
        // Rest at (latestCompetitorMax + 1) - anything later is either our fish or a ghost we shouldn't wait for
        var latestCompetitorMax = sameTug.Max(c => c.BiteMax);
        if (latestCompetitorMax < target.Signals.BiteTimeMin)
            return latestCompetitorMax + 1;

        // Short-bite target pattern (e.g. Fae Rainbow): our window is tiny; bail right after it
        if (target.Signals.BiteTimeMax <= 12)
            return target.Signals.BiteTimeMax + 1;

        return null;
    }

    public static List<TugType> HookOnlyTugs(FishProfile target, IReadOnlyList<PoolMember> pool) {
        // Sparse !!! pool (≤2 legendaries including us): only hook !!!. Letting ! / !! go preserves slap and GP
        if (target.Signals.Tug == TugType.Legendary) {
            var legendaries = pool.Count(p => p.Tug == TugType.Legendary);
            if (legendaries <= 2)
                return [TugType.Legendary];
        }

        // Intuition fish: only the tug that can be the target under intuition (or the prep fish we're farming)
        // Today that's simply the target's tug - preset builder layers prep vs intuition hooksets separately
        if (target.Acquisition.Type is AcquisitionType.IntuitionStraight or AcquisitionType.IntuitionMooch)
            return [target.Signals.Tug];

        return [target.Signals.Tug];
    }

    // Rough P(this fish | this pool) from rate tiers. Used by prep-time estimates, not for bait scoring
    // (bait scoring prefers histograms via SpotBaitPoolRateEstimator when available)
    public static double EstimateBiteProbability(PoolMember member, IReadOnlyList<PoolMember> pool) {
        var eligible = pool.Where(p => p.RateTier != RateTier.Unknown).ToList();
        if (eligible.Count == 0)
            return 1.0 / Math.Max(pool.Count, 1);

        var weight = TierWeight(member.RateTier);
        var total = eligible.Sum(p => TierWeight(p.RateTier));
        return weight / Math.Max(total, 1);
    }

    private static double TierWeight(RateTier tier) => tier switch {
        RateTier.Common => 4.0,
        RateTier.Uncommon => 2.0,
        RateTier.Rare => 1.0,
        RateTier.VeryRare => 0.25,
        _ => 1.0,
    };
}
