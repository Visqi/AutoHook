using AutoHook.FishSolverIntegration;
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
    public static List<BaitFishClass> LureFishes { get; private set; } = [];
    public static List<BaitFishClass> MoochableFish { get; private set; } = [];
    public static List<ImportedFish> ImportedFishes { get; private set; } = [];
    public static List<ImportedFish> SpearfishFishes { get; private set; } = [];
    public static HashSet<uint> SpearfishItemIds { get; private set; } = [];
    public static IReadOnlyDictionary<uint, ImportedFish> SpearfishFishesByItemId { get; private set; }
        = new Dictionary<uint, ImportedFish>();
    public static IReadOnlyDictionary<uint, SpearfishingPoolRef> SpearfishingPoolsByNotebookId { get; private set; }
        = new Dictionary<uint, SpearfishingPoolRef>();
    public static IReadOnlyDictionary<uint, IReadOnlyList<uint>> SpearfishingNotebookIdsByItemId { get; private set; }
        = new Dictionary<uint, IReadOnlyList<uint>>();
    public static IReadOnlyDictionary<uint, SpearfishingSpotRef> SpearfishingSpotsByPointId { get; private set; }
        = new Dictionary<uint, SpearfishingSpotRef>();
    public static List<uint> FishingStatuses { get; private set; } = [];
    public static FishSolverBridge FishSolver { get; private set; } = new();

    public readonly record struct SpearfishingSpotRef(uint GatheringPointId, uint GatheringPointBaseId, uint NotebookId, bool IsShadowNode);
    public readonly record struct SpearfishingPoolRef(uint NotebookId, uint GatheringPointBaseId, string Name, bool IsShadowNode, IReadOnlyList<uint> ItemIds);

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

        LureFishes = [.. Fishes.Where(f => f.LureMessage != "")];

        MoochableFish = FindRows<FishingBaitParameter>(x => x.Item.Value.ItemUICategory.RowId != 33).Select(f => new BaitFishClass(f.Item.RowId)).ToList() ?? [];

        try {
            var fishList = Path.Combine(Svc.Interface.AssemblyLocation.DirectoryName!, $"Data\\FishData\\fish_list.json");

            if (File.Exists(fishList)) {
                ImportedFishes = JsonSerializer.Deserialize<List<ImportedFish>>(File.ReadAllText(fishList))!;
                FishSolver.EnsureLoaded(fishList);
            }

            var spearfishingRows = Svc.Data.GetExcelSheet<SpearfishingItem>()
                .Where(row => row.Item.RowId != 0)
                .ToList();

            SpearfishFishes =
            [
                .. spearfishingRows
                    .Join(ImportedFishes, row => (int)row.Item.RowId, f => f.ItemId, (_, match) => new ImportedFish {
                        ItemId = match.ItemId,
                        IsSpearFish = true,
                        Size = match.Size,
                        Speed = match.Speed,
                    })
                    .GroupBy(fish => fish.ItemId)
                    .Select(group => group.First())
            ];
            SpearfishItemIds = [.. spearfishingRows.Select(row => row.Item.RowId)];
            SpearfishFishesByItemId = SpearfishFishes.ToDictionary(fish => (uint)fish.ItemId);

            var (pools, notebooksByItem) = BuildSpearfishingPoolIndexes(spearfishingRows);
            SpearfishingPoolsByNotebookId = pools;
            SpearfishingNotebookIdsByItemId = notebooksByItem;
            SpearfishingSpotsByPointId = BuildSpearfishingSpotMap(pools);
        }
        catch (Exception e) {
            ImGui.SetClipboardText(e.Message);
            Svc.Log.Error(e, "[GameRes] Init failed.");
        }
    }

    private static (Dictionary<uint, SpearfishingPoolRef> Pools, Dictionary<uint, IReadOnlyList<uint>> NotebooksByItem) BuildSpearfishingPoolIndexes(IReadOnlyList<SpearfishingItem> spearfishingRows) {
        var itemIdBySpearfishingRowId = spearfishingRows.GroupBy(row => row.RowId).ToDictionary(group => group.Key, group => group.First().Item.RowId);

        var pools = new Dictionary<uint, SpearfishingPoolRef>();
        foreach (var notebook in Svc.Data.GetExcelSheet<SpearfishingNotebook>()) {
            var baseId = notebook.GatheringPointBase.RowId;
            if (baseId == 0)
                continue;

            var itemIds = notebook.GatheringPointBase.Value.Item
                .Select(item => itemIdBySpearfishingRowId.GetValueOrDefault(item.RowId))
                .Where(itemId => itemId != 0)
                .Distinct()
                .OrderBy(id => id)
                .ToList();
            pools[notebook.RowId] = new SpearfishingPoolRef(notebook.RowId, baseId, notebook.PlaceName.Value.Name.ToString(), notebook.IsShadowNode, itemIds);
        }

        var notebooksByItem = pools.Values
            .SelectMany(pool => pool.ItemIds.Select(itemId => (itemId, pool.NotebookId)))
            .GroupBy(entry => entry.itemId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<uint>)[.. group.Select(entry => entry.NotebookId).Distinct().OrderBy(id => id)]);

        return (pools, notebooksByItem);
    }

    private static Dictionary<uint, SpearfishingSpotRef> BuildSpearfishingSpotMap(
        IReadOnlyDictionary<uint, SpearfishingPoolRef> pools) {
        var poolByBaseId = pools.Values
            .Where(pool => pool.GatheringPointBaseId != 0)
            .GroupBy(pool => pool.GatheringPointBaseId)
            .ToDictionary(group => group.Key, group => group.First());

        var map = new Dictionary<uint, SpearfishingSpotRef>();
        foreach (var point in Svc.Data.GetExcelSheet<GatheringPoint>()) {
            var baseId = point.GatheringPointBase.RowId;
            if (baseId == 0 || !poolByBaseId.TryGetValue(baseId, out var pool))
                continue;
            map[point.RowId] = new SpearfishingSpotRef(point.RowId, baseId, pool.NotebookId, pool.IsShadowNode);
        }

        return map;
    }
}
