using AutoHook.Spearfishing.Enums;

namespace AutoHook.Presets.Bases;

/// <summary>JSON DTO for fish_list.json. Runtime catalog entries are <see cref="Data.Fish"/>.</summary>
public class ImportedFish {
    public int ItemId { get; set; }
    public HookType HookType { get; set; }
    public BiteType BiteType { get; set; }
    public int InitialBait { get; set; }
    public List<int> Mooches { get; set; } = [];
    public List<FishPredator> Predators { get; set; } = [];
    public bool IsSpearFish { get; set; }
    public SpearfishSize Size { get; set; }
    public SpearfishSpeed Speed { get; set; }
    public bool OceanFish { get; set; }
    public List<int> SpotIds { get; set; } = [];
    public List<int> Weathers { get; set; } = [];
    public List<int> WeathersFrom { get; set; } = [];
    public double? Spawn { get; set; }
    public double? Duration { get; set; }
    public string? Time { get; set; }
    public int? MinGathering { get; set; }
    public bool Snagging { get; set; }
    public int MLure { get; set; }
    public int ALure { get; set; }
    public int? OceanFishingTime { get; set; }
    public string? FruityVideo { get; set; }
    public double BiteTimeMin { get; set; }
    public double BiteTimeMax { get; set; }

    public class FishPredator {
        public int ItemId { get; set; }
        public int Quantity { get; set; }
    }
}
