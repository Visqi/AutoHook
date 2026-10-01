using AutoHook.Spearfishing.Enums;
using Lumina.Excel.Sheets;

namespace AutoHook.Data;

public sealed class Fish {
    public int Id { get; init; }
    public uint ItemId => Id > 0 ? (uint)Id : 0;

    public string Name { get; init; } = "";
    public string Label => Id > 0 ? $"[#{Id}] {Name}" : Name;

    public bool IsBait { get; init; }
    public bool IsCatchable { get; init; }
    public bool IsMoochable { get; init; }
    public bool IsSpearFish { get; init; }
    public bool IsLureFish { get; init; }
    public bool IsOceanFish { get; init; }
    public bool IsSentinel => Id is FishBaitCatalog.AllBaitsId or FishBaitCatalog.AllMoochesId or <= 0;

    public BaitType BaitType { get; init; } = BaitType.Unknown;
    public string LureMessage { get; init; } = "";
    public bool IsLocked {
        get {
            if (Id <= 0 || IsSentinel) return false;
            var row = FishParameter.FirstOrNull(r => r.Item.RowId == Id);
            return row is { GatheringSubCategory.ValueNullable.Item.RowId: not 0, GatheringSubCategory.ValueNullable.Item.Value: var book } && !IUnlockState.Get().IsItemUnlocked(book);
        }
    }

    public HookType HookType { get; init; }
    public BiteType BiteType { get; init; }
    public int InitialBait { get; init; }
    public IReadOnlyList<int> Mooches { get; init; } = [];
    public IReadOnlyList<FishPredator> Predators { get; init; } = [];
    public SpearfishSize Size { get; init; }
    public SpearfishSpeed Speed { get; init; }
    public IReadOnlyList<int> SpotIds { get; init; } = [];
    public IReadOnlyList<int> Weathers { get; init; } = [];
    public IReadOnlyList<int> WeathersFrom { get; init; } = [];
    public double? Spawn { get; init; }
    public double? Duration { get; init; }
    public string? Time { get; init; }
    public int? MinGathering { get; init; }
    public bool Snagging { get; init; }
    public int MLure { get; init; }
    public int ALure { get; init; }
    public int? OceanFishingTime { get; init; }
    public string? FruityVideo { get; init; }
    public double BiteTimeMin { get; init; }
    public double BiteTimeMax { get; init; }
    public bool HasImportedData { get; init; }

    public override string ToString() => Label;

    public sealed class FishPredator {
        public int ItemId { get; init; }
        public int Quantity { get; init; }
    }
}
