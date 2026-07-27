using AutoHook.FishSolver.Models;

namespace AutoHook.FishSolver.Engine;

// what to do when a bite occurs (or doesn't)
// priority:
// 1. if current tug is the target's tug -> use the appropriate hookset (obviously)
// 2. if tug types are filtered and this isn't one of them, LetGo/Rest to preserve slap/IC
// 3. if the bite timer crossed early-cancel, then Rest. Optionally Lure instead when eligible
public static class CastDecisionPolicyBuilder {
    public static List<CastDecisionRule> Build(FishProfile profile, InferredTactics tactics, PlayerProfile player) {
        var rules = new List<CastDecisionRule>();
        var targetTug = profile.Signals.Tug;
        var hookAction = profile.Signals.Hookset switch {
            HooksetType.Precision => HookActionKind.PrecisionHookset,
            HooksetType.Powerful => HookActionKind.PowerfulHookset,
            _ => HookActionKind.Hook,
        };

        foreach (var tug in tactics.HookOnlyTugs) {
            rules.Add(new CastDecisionRule {
                WhenTug = tug,
                Action = tug == targetTug ? hookAction : HookActionKind.LetGo,
            });
        }

        if (tactics.EarlyCancelSec is { } cancel) {
            rules.Add(new CastDecisionRule {
                BeforeSecondsMax = cancel,
                Action = HookActionKind.Rest,
            });

            // Lure as a cheaper Rest when eligible
            if (PickMatchingLure(profile, player) is { } lure)
                rules.Add(new CastDecisionRule {
                    BeforeSecondsMax = cancel,
                    Action = lure,
                });
        }
        else if (tactics.Archetype == StrategyArchetype.LureStack
                 && PickMatchingLure(profile, player) is { } stackLure) {
            // Lure Stack: stack matching lure every cast (no early-cancel timer needed)
            rules.Add(new CastDecisionRule { Action = stackLure });
        }

        // other tugs: LetGo if slap (don't burn it), else Rest
        foreach (var tug in Enum.GetValues<TugType>()) {
            if (tug == TugType.Unknown || tactics.HookOnlyTugs.Contains(tug))
                continue;
            rules.Add(new CastDecisionRule {
                WhenTug = tug,
                Action = player.Skills.SurfaceSlap ? HookActionKind.LetGo : HookActionKind.Rest,
            });
        }

        return rules;
    }

    public static HookActionKind? PickMatchingLure(FishProfile profile, PlayerProfile player)
        => profile.Signals.Hookset switch {
            HooksetType.Powerful when player.Skills.AmbitiousLure => HookActionKind.AmbitiousLure,
            HooksetType.Precision when player.Skills.ModestLure => HookActionKind.ModestLure,
            _ => null,
        };
}

// GP spend order when GP is tight
// IC prep is hungry (IC + slap + chum) - reserve enough to still IC the last trigger
// swimbait bank is cheaper (Spareful Hand free); default grind: slap -> chum -> Patience II -> Makeshift
// PatienceMinGp: don't cast P2 at bare cost — leave headroom for hookset (+ lure if in play)
//
// mid-window recovery (preset already loops these; not emitted as output):
// - intuition drops -> rebuild triggers (IC zero-time: N-1 then re-IC)
// - mooch dies -> recatch mooch fish (Patience II / Prize Catch help)
// - swimbait empty -> back to mooch bait + Spareful Hand
// - slap falls off -> recatch slap target
public static class ResourcePolicyBuilder {
    private const int PatienceIICost = 560;
    private const int AfterPatienceHeadroom = 50; // hookset + a bit of regen buffer
    private const int LureBudget = 50;            // room for a lure or two after P2

    public static ResourcePolicy Build(FishProfile profile, InferredTactics tactics, PlayerProfile player) {
        var priority = new List<FisherSkill>();
        if (tactics.HoldMode == PrepHoldMode.SwimbaitBank && player.Skills.SparefulHand)
            priority.AddRange([FisherSkill.SparefulHand, FisherSkill.PrizeCatch, FisherSkill.SurfaceSlap, FisherSkill.Chum]);
        else if (tactics.HoldMode == PrepHoldMode.IdenticalCastZeroTime)
            priority.AddRange([FisherSkill.IdenticalCast, FisherSkill.SurfaceSlap, FisherSkill.Chum]);
        else
            priority.AddRange([FisherSkill.SurfaceSlap, FisherSkill.Chum, FisherSkill.PatienceII, FisherSkill.MakeshiftBait]);

        if (profile.Signals.Hookset == HooksetType.Powerful && player.Skills.AmbitiousLure && (tactics.Archetype == StrategyArchetype.LureStack || profile.Eligibility.ALureEligible || tactics.EarlyCancelSec.HasValue))
            priority.Add(FisherSkill.AmbitiousLure);
        else if (profile.Signals.Hookset == HooksetType.Precision && player.Skills.ModestLure && (tactics.Archetype == StrategyArchetype.LureStack || profile.Eligibility.MLureEligible || tactics.EarlyCancelSec.HasValue))
            priority.Add(FisherSkill.ModestLure);
        else {
            if (player.Skills.AmbitiousLure && profile.Eligibility.ALureEligible)
                priority.Add(FisherSkill.AmbitiousLure);
            if (player.Skills.ModestLure && profile.Eligibility.MLureEligible)
                priority.Add(FisherSkill.ModestLure);
        }

        var gpReserve = tactics.HoldMode switch {
            PrepHoldMode.IdenticalCastZeroTime => 400, // IC + a bit of headroom
            PrepHoldMode.SwimbaitBank => 200,          // slap budget
            _ => player.Skills.SurfaceSlap ? 200 : 0,
        };

        var expectLure = ExpectsLureSpend(profile, tactics, player);
        var patienceMinGp = player.Skills.PatienceII ? PatienceIICost + AfterPatienceHeadroom + (expectLure ? LureBudget : 0) : 0;
        return new ResourcePolicy {
            GpPriority = priority,
            UseChum = true,
            UseCordials = player.Assumptions.UseCordials,
            GpReserve = gpReserve,
            PatienceMinGp = patienceMinGp,
        };
    }

    private static bool ExpectsLureSpend(FishProfile profile, InferredTactics tactics, PlayerProfile player) {
        if (tactics.Archetype is StrategyArchetype.LureStack or StrategyArchetype.LureReroll or StrategyArchetype.ShortBiteReset)
            return true;
        if (tactics.EarlyCancelSec.HasValue && CastDecisionPolicyBuilder.PickMatchingLure(profile, player) != null)
            return true;
        return player.Skills.AmbitiousLure && profile.Eligibility.ALureEligible || player.Skills.ModestLure && profile.Eligibility.MLureEligible;
    }
}
