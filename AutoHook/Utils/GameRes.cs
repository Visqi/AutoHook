using Dalamud.Bindings.ImGui;
using Lumina.Excel.Sheets;
using System.IO;
using System.Reflection;
using System.Text.Json;

namespace AutoHook.Utils;

public static class GameRes {
    public const uint FishingTackleRow = 30;
    public const int AllBaitsId = -99;
    public const int AllMoochesId = -98;

    public static List<BaitFishClass> Baits { get; private set; } = [];
    public static List<BaitFishClass> Fishes { get; private set; } = [];
    public static List<BaitFishClass> LureFishes => [.. Fishes.Where(f => f.LureMessage != "")];
    public static List<BaitFishClass> MoochableFish { get; private set; } = [];
    public static List<ImportedFish> ImportedFishes { get; private set; } = [];
    public static List<ImportedFish> SpearfishFishes { get; private set; } = [];
    public static List<BiteTimers> BiteTimers { get; private set; } = [];
    public static List<uint> FishingStatuses { get; private set; } = [];

    public static void Initialize() {
        FishingStatuses = [.. typeof(IDs.Status).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Select(f => f.GetValue(null))
            .OfType<uint>()
            .Where(id => id != 0)
            .OrderBy(id => id)];

        Baits = [.. FindRows<Item>(i => i.ItemSearchCategory.RowId == FishingTackleRow).ToList()
            .Concat([.. FindRows<WKSItemInfo>(i => i.WKSItemSubCategory.RowId == 5).Select(i => i.Item.Value)])
            .Select(b => new BaitFishClass(b))];

        Fishes = FindRows<FishParameter>(f => f.Item.RowId is not 0 and < 1000000)
            .Select(f => new BaitFishClass(f)).GroupBy(f => f.Id).Select(group => group.First()).ToList() ?? [];

        MoochableFish = FindRows<FishingBaitParameter>(x => x.Item.Value.ItemUICategory.RowId != 33).Select(f => new BaitFishClass(f.Item.RowId)).ToList() ?? [];

        try {
            var fishList = Path.Combine(Svc.Interface.AssemblyLocation.DirectoryName!, $"Data\\FishData\\fish_list.json");

            if (File.Exists(fishList)) {
                ImportedFishes = JsonSerializer.Deserialize<List<ImportedFish>>(File.ReadAllText(fishList))!;
            }

            var byId = ImportedFishes.ToDictionary(f => f.ItemId, f => f);
            var list = new List<ImportedFish>();

            foreach (var row in Svc.Data.GetExcelSheet<SpearfishingItem>()) {
                // fish_list is wrong when it comes to most timeworn maps not being spearfish so build a list of actual spearfish and match fish_list to it
                var itemId = (int)row.Item.RowId;
                if (itemId == 0) continue;

                if (byId.GetValueOrDefault(itemId) is { } match) {
                    list.Add(new ImportedFish {
                        ItemId = itemId,
                        IsSpearFish = true,
                        Size = match.Size,
                        Speed = match.Speed,
                    });
                }
            }

            SpearfishFishes = list;

            var biteTimers = Path.Combine(Svc.Interface.AssemblyLocation.DirectoryName!, $"Data\\FishData\\bitetimers.json");
            if (File.Exists(biteTimers)) {
                BiteTimers = JsonSerializer.Deserialize<List<BiteTimers>>(File.ReadAllText(biteTimers))!;
            }
        }
        catch (Exception e) {
            ImGui.SetClipboardText(e.Message);
            Svc.Log.Error($"{e.Message}");
        }
    }
}
