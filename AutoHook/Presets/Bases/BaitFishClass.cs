using ECommons.MathHelpers;
using Newtonsoft.Json;
using ItemRow = Lumina.Excel.Sheets.Item;

namespace AutoHook.Presets.Bases;

/// <summary>Config/UI identity for a bait or fish. Resolves display data via <see cref="FishBaitCatalog"/>.</summary>
public class BaitFishClass : IComparable<BaitFishClass> {
    [JsonIgnore]
    public string Name {
        get {
            if (!string.IsNullOrEmpty(field))
                return field;
            return Id switch {
                FishBaitCatalog.AllMoochesId => UIStrings.All_Mooches,
                FishBaitCatalog.AllBaitsId => UIStrings.All_Baits,
                <= 0 => UIStrings.None,
                _ => FishBaitCatalog.Get()[Id]?.Name ?? ItemRow.GetRow((uint)Id).Name.ToString(),
            };
        }
    } = "";

    [JsonIgnore]
    public string Label => FishBaitCatalog.Get()[Id]?.Label ?? (Id > 0 ? $"[#{Id}] {Name}" : Name);

    [JsonIgnore]
    public bool IsLocked => FishBaitCatalog.Get()[Id]?.IsLocked ?? false;

    [JsonIgnore]
    public int MinGathering => FishBaitCatalog.Get()[Id]?.MinGathering ?? 0;

    public int Id;

    [JsonIgnore]
    public string LureMessage => FishBaitCatalog.Get()[Id]?.LureMessage ?? "";

    [JsonIgnore]
    public BaitType BaitType => FishBaitCatalog.Get()[Id]?.BaitType ?? BaitType.Unknown;

    [JsonIgnore]
    public Fish? CatalogFish => FishBaitCatalog.Get()[Id];

    public BaitFishClass(ItemRow data) {
        Id = (int)data.RowId;
    }

    public BaitFishClass(Lumina.Excel.Sheets.FishParameter fishRow) {
        var itemData = fishRow.Item.GetValueOrDefault<ItemRow>() ?? new ItemRow();
        Id = (int)itemData.RowId;
    }

    public BaitFishClass(string name, int id) {
        Id = id;
        Name = name;
    }

    public BaitFishClass() {
        Id = -1;
    }

    public BaitFishClass(Number id) {
        Id = id;
    }

    public BaitFishClass(Fish fish) {
        Id = fish.Id;
    }

    public int CompareTo(BaitFishClass? other)
        => Id.CompareTo(other?.Id ?? 0);
}
