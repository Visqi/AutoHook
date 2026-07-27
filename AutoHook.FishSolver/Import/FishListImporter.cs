using System.Text.Json;
using AutoHook.FishSolver.Engine;
using AutoHook.FishSolver.Models;

namespace AutoHook.FishSolver.Import;

public static class FishListImporter {
    private static readonly JsonSerializerOptions JsonOptions = new() {
        PropertyNameCaseInsensitive = true,
    };

    public static List<FishRecord> LoadFromFile(string path) {
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<List<FishRecord>>(json, JsonOptions) ?? [];
    }

    public static List<FishRecord> LoadFromJson(string json)
        => JsonSerializer.Deserialize<List<FishRecord>>(json, JsonOptions) ?? [];
}

public static class SolverOverridesMerger {
    private static readonly JsonSerializerOptions JsonOptions = new() {
        PropertyNameCaseInsensitive = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
    };

    public static List<FishOverride> LoadFromEmbedded() {
        try {
            return EmbeddedDataLoader.Load<List<FishOverride>>("solver_overrides.json");
        }
        catch {
            return [];
        }
    }

    public static List<FishOverride> LoadFromFile(string? path) {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return [];
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<List<FishOverride>>(json, JsonOptions) ?? [];
    }

    public static void ApplyOverrides(FishKnowledgeBase kb, IEnumerable<FishOverride> overrides) {
        foreach (var ovr in overrides) {
            if (!kb.FishById.TryGetValue(ovr.FishId, out var profile))
                continue;

            var eligibility = CloneEligibility(profile.Eligibility);
            var acquisition = profile.Acquisition;
            var pool = profile.PoolAtPrimarySpot;
            var tactics = profile.Tactics;

            if (ovr.BaitId is { } baitId && baitId > 0) {
                eligibility = CloneEligibility(eligibility, baitId: baitId);
                var spot = eligibility.SpotIds.FirstOrDefault();
                pool = spot > 0 ? kb.GetPool(spot, baitId)?.Members.ToList() ?? [] : [];
                // override bait is often omitted so keep an empty pool so that lure exclusivity checks still work
                if (pool.Count == 0) {
                    pool = [
                        new PoolMember {
                            FishId = profile.FishId,
                            Tug = profile.Signals.Tug,
                            Hookset = profile.Signals.Hookset,
                            BiteMin = profile.Signals.BiteTimeMin,
                            BiteMax = profile.Signals.BiteTimeMax,
                            RateTier = RateTier.Unknown,
                        },
                    ];
                }
            }

            if (ovr.ALureEligible is { } aLure)
                eligibility = CloneEligibility(eligibility, aLureEligible: aLure);
            if (ovr.MLureEligible is { } mLure)
                eligibility = CloneEligibility(eligibility, mLureEligible: mLure);

            if (ovr.ClearMoochChain) {
                acquisition = acquisition with {
                    MoochChain = [],
                    Type = acquisition.Predators.Count > 0 ? acquisition.Type : AcquisitionType.StraightCatch,
                };
            }

            if (ovr.IntuitionDurationSec is { } dur)
                acquisition = acquisition with { IntuitionDurationSec = dur };

            // stash forced tactics on the profile, engine re-applies after Classify so they stick
            if (ovr.Archetype is { } archetype)
                tactics = tactics with { Archetype = archetype };
            if (ovr.HoldMode is { } holdMode)
                tactics = tactics with { HoldMode = holdMode };
            if (ovr.SlapTargetFishId is { } slap)
                tactics = tactics with { SlapTargetFishId = slap };
            if (ovr.EarlyCancelSec is { } cancel)
                tactics = tactics with { EarlyCancelSec = cancel };

            kb.FishById[ovr.FishId] = profile with {
                Eligibility = eligibility,
                Acquisition = acquisition,
                PoolAtPrimarySpot = pool,
                Tactics = tactics,
            };
        }

        kb.Overrides.AddRange(overrides);
    }

    // apply after Classify otherwise this gets overwritten
    public static InferredTactics ApplyForcedTactics(FishKnowledgeBase kb, int fishId, InferredTactics tactics) {
        var ovr = kb.Overrides.FirstOrDefault(o => o.FishId == fishId);
        if (ovr == null)
            return tactics;

        if (ovr.Archetype is { } archetype)
            tactics = tactics with { Archetype = archetype };
        if (ovr.HoldMode is { } holdMode)
            tactics = tactics with {
                HoldMode = holdMode,
                RequiresContinuousFishing = PrepHoldModeSelector.RequiresContinuousFishing(holdMode),
                StowRodSafeDuringHold = holdMode == PrepHoldMode.CrossWindowJail,
            };
        if (ovr.SlapTargetFishId is { } slap)
            tactics = tactics with { SlapTargetFishId = slap };
        if (ovr.EarlyCancelSec is { } cancel)
            tactics = tactics with { EarlyCancelSec = cancel };

        return tactics;
    }

    private static FishEligibility CloneEligibility(
        FishEligibility src,
        int? baitId = null,
        bool? aLureEligible = null,
        bool? mLureEligible = null)
        => new() {
            FishId = src.FishId,
            SpotIds = src.SpotIds,
            BaitId = baitId ?? src.BaitId,
            SpawnHour = src.SpawnHour,
            DurationHours = src.DurationHours,
            TimeRange = src.TimeRange,
            Weathers = src.Weathers,
            WeathersFrom = src.WeathersFrom,
            MinGathering = src.MinGathering,
            SnaggingRequired = src.SnaggingRequired,
            MLureEligible = mLureEligible ?? src.MLureEligible,
            ALureEligible = aLureEligible ?? src.ALureEligible,
        };
}
