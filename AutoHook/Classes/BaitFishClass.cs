using System.Text.Json.Serialization;
using AutoHook.Enums;
using AutoHook.Resources.Localization;
using AutoHook.Utils;
using FishRow = Lumina.Excel.Sheets.FishParameter;
using ItemRow = Lumina.Excel.Sheets.Item;

namespace AutoHook.Classes;

public class BaitFishClass : IComparable<BaitFishClass>
{
    [JsonIgnore]
    public string Name => Id switch
    {
        GameRes.AllMoochesId => UIStrings.All_Mooches,
        GameRes.AllBaitsId => UIStrings.All_Baits,
        _ => MultiString.GetItemName((uint)Id)
    };

    public int Id;

    [JsonIgnore] public string LureMessage = "";

    // check the bait type
    [JsonIgnore]
    public BaitType BaitType
    {
        get
        {
            return GameRes.Baits.Any(b => b.Id == Id) ? BaitType.Bait :
                GameRes.Fishes.Any(f => f.Id == Id) ? BaitType.Mooch : BaitType.Unknown;
        }
    }

    public BaitFishClass(ItemRow data)
    {
        Id = (int)data.RowId;
    }

    public BaitFishClass(FishRow fishRow)
    {
        var itemData = fishRow.Item.GetValueOrDefault<ItemRow>() ?? new ItemRow();
        LureMessage = fishRow.Unknown_70_1.ToString();
        Id = (int)itemData.RowId;
    }

    public BaitFishClass(string name, int id)
    {
        Id = id;
    }

    public BaitFishClass()
    {
        Id = -1;
    }

    public BaitFishClass(int id)
    {
        Id = id;
    }

    public int CompareTo(BaitFishClass? other)
        => Id.CompareTo(other?.Id ?? 0);
}