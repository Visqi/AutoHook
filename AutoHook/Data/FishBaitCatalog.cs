using AutoHook.FishSolverIntegration;
using Lumina.Excel.Sheets;
using System.Collections.Frozen;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using FishRow = Lumina.Excel.Sheets.FishParameter;
using ItemRow = Lumina.Excel.Sheets.Item;

namespace AutoHook.Data;

public sealed class FishBaitCatalog : IPluginService {
    public const uint FishingTackleRow = 30;
    public const int AllBaitsId = -99;
    public const int AllMoochesId = -98;

    private Dictionary<int, Fish> _byId = [];
    private FrozenSet<uint> _spearItemIds = [];
    private volatile bool _ready;

    public bool IsReady => _ready;
    public FishSolverBridge FishSolver { get; private set; } = new();
    public IReadOnlyList<uint> FishingStatuses { get; private set; } = [];
    public IReadOnlyList<Fish> Baits { get; private set; } = [];
    public IReadOnlyList<Fish> Fishes { get; private set; } = [];
    public IReadOnlyList<Fish> LureFishes { get; private set; } = [];
    public IReadOnlyList<Fish> MoochableFish { get; private set; } = [];
    public IReadOnlyList<Fish> SpearFishes { get; private set; } = [];
    public IReadOnlyList<Fish> ImportedFishes { get; private set; } = [];
    public IReadOnlySet<uint> SpearfishItemIds => _spearItemIds;

    public Fish? this[int id] => _byId.TryGetValue(id, out var fish) ? fish : null;
    public Fish? this[uint id] => this[(int)id];

    public bool TryGet(int id, out Fish fish) => _byId.TryGetValue(id, out fish!);
    public bool TryGet(uint id, out Fish fish) => TryGet((int)id, out fish);

    public async Task InitializeAsync(CancellationToken cancellationToken = default) {
        if (_ready)
            return;

        var fishListPath = Path.Combine(Svc.Interface.AssemblyLocation.DirectoryName!, "Data", "FishData", "fish_list.json");
        var built = await Task.Run(() => Build(fishListPath, cancellationToken), cancellationToken).ConfigureAwait(true);

        _byId = built.ById;
        Baits = built.Baits;
        Fishes = built.Fishes;
        LureFishes = built.LureFishes;
        MoochableFish = built.Moochable;
        SpearFishes = built.SpearFishes;
        ImportedFishes = built.Imported;
        _spearItemIds = built.SpearItemIds;
        FishingStatuses = built.FishingStatuses;
        FishSolver = built.FishSolver;
        _ready = true;
    }

    private static BuiltCatalog Build(string fishListPath, CancellationToken ct) {
        ct.ThrowIfCancellationRequested();

        var fishingStatuses = typeof(IDs.Status).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Select(f => f.GetValue(null))
            .OfType<uint>()
            .Where(id => id != 0)
            .OrderBy(id => id)
            .ToList();

        var byId = new Dictionary<int, Fish>();

        void Upsert(Fish fish) => byId[fish.Id] = byId.TryGetValue(fish.Id, out var existing) ? Merge(existing, fish) : fish;

        foreach (var item in FindRows<ItemRow>(i => i.ItemSearchCategory.RowId == FishingTackleRow)) {
            Upsert(new Fish {
                Id = (int)item.RowId,
                Name = item.Name.ToString(),
                IsBait = true,
                BaitType = BaitType.Bait,
            });
        }

        foreach (var wks in FindRows<WKSItemInfo>(i => i.WKSItemSubCategory.RowId == 5)) {
            var item = wks.Item.Value;
            if (item.RowId == 0)
                continue;
            Upsert(new Fish {
                Id = (int)item.RowId,
                Name = item.Name.ToString(),
                IsBait = true,
                BaitType = BaitType.Bait,
            });
        }

        foreach (var fishRow in FindRows<FishRow>(f => f.Item.RowId is not 0 and < 1000000)) {
            var item = fishRow.Item.GetValueOrDefault<ItemRow>() ?? default;
            var id = (int)item.RowId;
            if (id <= 0)
                continue;
            var lure = fishRow.Unknown_70_1.ToString();
            Upsert(new Fish {
                Id = id,
                Name = item.Name.ToString(),
                IsCatchable = true,
                BaitType = BaitType.Mooch,
                LureMessage = lure,
                IsLureFish = !string.IsNullOrEmpty(lure),
                IsLocked = Fish.ComputeIsLocked((uint)id),
            });
        }

        var moochableIds = FindRows<FishingBaitParameter>(x => x.Item.Value.ItemUICategory.RowId != 33).Select(f => (int)f.Item.RowId).Distinct().ToHashSet();
        foreach (var id in moochableIds) {
            if (byId.TryGetValue(id, out var fish)) {
                Upsert(new Fish { Id = id, Name = fish.Name, IsMoochable = true, IsCatchable = true, BaitType = BaitType.Mooch });
            }
            else {
                Upsert(new Fish {
                    Id = id,
                    Name = ItemRow.GetRow((uint)id).Name.ToString(),
                    IsMoochable = true,
                    IsCatchable = true,
                    BaitType = BaitType.Mooch,
                });
            }
        }

        var fishSolver = new FishSolverBridge();
        List<ImportedFish> importedDtos = [];
        if (File.Exists(fishListPath)) {
            importedDtos = JsonSerializer.Deserialize<List<ImportedFish>>(File.ReadAllText(fishListPath)) ?? [];
            fishSolver.EnsureLoaded(fishListPath);
        }

        foreach (var dto in importedDtos) {
            ct.ThrowIfCancellationRequested();
            Upsert(FromImported(dto, byId.GetValueOrDefault(dto.ItemId)));
        }

        var spearfishingRows = SpearfishingItem.Where(row => row.Item.RowId != 0).ToList();
        var spearItemIds = spearfishingRows.Select(row => row.Item.RowId).ToFrozenSet();
        var importedById = importedDtos.GroupBy(f => f.ItemId).ToDictionary(g => g.Key, g => g.First());

        foreach (var itemId in spearItemIds) {
            ct.ThrowIfCancellationRequested();
            var id = (int)itemId;
            importedById.TryGetValue(id, out var match);
            if (byId.TryGetValue(id, out var existing)) {
                Upsert(new Fish {
                    Id = id,
                    Name = existing.Name,
                    IsSpearFish = true,
                    IsCatchable = true,
                    Size = match?.Size ?? existing.Size,
                    Speed = match?.Speed ?? existing.Speed,
                    HasImportedData = existing.HasImportedData || match != null,
                });
            }
            else if (match != null) {
                Upsert(Merge(FromImported(match, null), new Fish { Id = id, Name = ItemRow.GetRow(itemId).Name.ToString(), IsSpearFish = true, IsCatchable = true }));
            }
            else {
                Upsert(new Fish {
                    Id = id,
                    Name = ItemRow.GetRow(itemId).Name.ToString(),
                    IsSpearFish = true,
                    IsCatchable = true,
                });
            }
        }

        byId[AllBaitsId] = new Fish {
            Id = AllBaitsId,
            Name = UIStrings.All_Baits,
            IsBait = true,
            BaitType = BaitType.Bait,
        };
        byId[AllMoochesId] = new Fish {
            Id = AllMoochesId,
            Name = UIStrings.All_Mooches,
            IsMoochable = true,
            BaitType = BaitType.Mooch,
        };

        var all = byId.Values.OrderBy(f => f.Id).ToList();
        return new BuiltCatalog(
            byId,
            [.. all.Where(f => f.IsBait && f.Id > 0)],
            [.. all.Where(f => f.IsCatchable && !f.IsSpearFish && f.Id > 0)],
            [.. all.Where(f => f.IsLureFish && f.Id > 0)],
            [.. all.Where(f => f.IsMoochable && f.Id > 0)],
            [.. all.Where(f => f.IsSpearFish && f.Id > 0)],
            [.. all.Where(f => f.HasImportedData && f.Id > 0)],
            spearItemIds,
            fishingStatuses,
            fishSolver);
    }

    private static Fish FromImported(ImportedFish dto, Fish? existing) {
        var name = existing?.Name;
        if (string.IsNullOrEmpty(name) && dto.ItemId > 0)
            name = ItemRow.GetRow((uint)dto.ItemId).Name.ToString();

        return Merge(existing ?? new Fish { Id = dto.ItemId, Name = name ?? "" }, new Fish {
            Id = dto.ItemId,
            Name = name ?? dto.ItemId.ToString(),
            IsCatchable = true,
            IsSpearFish = dto.IsSpearFish || (existing?.IsSpearFish ?? false),
            IsOceanFish = dto.OceanFish,
            IsLureFish = existing?.IsLureFish ?? false,
            IsBait = existing?.IsBait ?? false,
            IsMoochable = existing?.IsMoochable ?? false,
            BaitType = existing?.BaitType is BaitType.Bait ? BaitType.Bait : BaitType.Mooch,
            LureMessage = existing?.LureMessage ?? "",
            IsLocked = existing?.IsLocked ?? false,
            HookType = dto.HookType,
            BiteType = dto.BiteType,
            InitialBait = dto.InitialBait,
            Mooches = dto.Mooches,
            Predators = [.. dto.Predators.Select(p => new Fish.FishPredator { ItemId = p.ItemId, Quantity = p.Quantity })],
            Size = dto.Size,
            Speed = dto.Speed,
            SpotIds = dto.SpotIds,
            Weathers = dto.Weathers,
            WeathersFrom = dto.WeathersFrom,
            Spawn = dto.Spawn,
            Duration = dto.Duration,
            Time = dto.Time,
            MinGathering = dto.MinGathering,
            Snagging = dto.Snagging,
            MLure = dto.MLure,
            ALure = dto.ALure,
            OceanFishingTime = dto.OceanFishingTime,
            FruityVideo = dto.FruityVideo,
            BiteTimeMin = dto.BiteTimeMin,
            BiteTimeMax = dto.BiteTimeMax,
            HasImportedData = true,
        });
    }

    private static Fish Merge(Fish a, Fish b) => new() {
        Id = b.Id != 0 ? b.Id : a.Id,
        Name = !string.IsNullOrEmpty(b.Name) ? b.Name : a.Name,
        IsBait = a.IsBait || b.IsBait,
        IsCatchable = a.IsCatchable || b.IsCatchable,
        IsMoochable = a.IsMoochable || b.IsMoochable,
        IsSpearFish = a.IsSpearFish || b.IsSpearFish,
        IsLureFish = a.IsLureFish || b.IsLureFish || !string.IsNullOrEmpty(b.LureMessage),
        IsOceanFish = a.IsOceanFish || b.IsOceanFish,
        BaitType = b.BaitType != BaitType.Unknown ? b.BaitType : a.BaitType,
        LureMessage = !string.IsNullOrEmpty(b.LureMessage) ? b.LureMessage : a.LureMessage,
        IsLocked = a.IsLocked || b.IsLocked,
        HookType = b.HasImportedData ? b.HookType : a.HookType,
        BiteType = b.HasImportedData ? b.BiteType : a.BiteType,
        InitialBait = b.HasImportedData ? b.InitialBait : a.InitialBait,
        Mooches = b.HasImportedData ? b.Mooches : a.Mooches,
        Predators = b.HasImportedData ? b.Predators : a.Predators,
        Size = b.Size != default ? b.Size : a.Size,
        Speed = b.Speed != default ? b.Speed : a.Speed,
        SpotIds = b.HasImportedData ? b.SpotIds : a.SpotIds,
        Weathers = b.HasImportedData ? b.Weathers : a.Weathers,
        WeathersFrom = b.HasImportedData ? b.WeathersFrom : a.WeathersFrom,
        Spawn = b.Spawn ?? a.Spawn,
        Duration = b.Duration ?? a.Duration,
        Time = b.Time ?? a.Time,
        MinGathering = b.MinGathering ?? a.MinGathering,
        Snagging = b.HasImportedData ? b.Snagging : a.Snagging,
        MLure = b.HasImportedData ? b.MLure : a.MLure,
        ALure = b.HasImportedData ? b.ALure : a.ALure,
        OceanFishingTime = b.OceanFishingTime ?? a.OceanFishingTime,
        FruityVideo = b.FruityVideo ?? a.FruityVideo,
        BiteTimeMin = b.HasImportedData ? b.BiteTimeMin : a.BiteTimeMin,
        BiteTimeMax = b.HasImportedData ? b.BiteTimeMax : a.BiteTimeMax,
        HasImportedData = a.HasImportedData || b.HasImportedData,
    };

    private readonly record struct BuiltCatalog(
        Dictionary<int, Fish> ById,
        IReadOnlyList<Fish> Baits,
        IReadOnlyList<Fish> Fishes,
        IReadOnlyList<Fish> LureFishes,
        IReadOnlyList<Fish> Moochable,
        IReadOnlyList<Fish> SpearFishes,
        IReadOnlyList<Fish> Imported,
        FrozenSet<uint> SpearItemIds,
        IReadOnlyList<uint> FishingStatuses,
        FishSolverBridge FishSolver);
}
