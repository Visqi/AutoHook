using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using AutoHook.Classes;
using Dalamud.Bindings.ImGui;
using ECommons;
using Lumina.Excel.Sheets;

namespace AutoHook.Utils;

public static class GameRes
{
    public const uint FishingTackleRow = 30;
    public const int AllBaitsId = -99;
    public const int AllMoochesId = -98;

    public static List<BaitFishClass> Baits { get; private set; } = new();
    public static List<BaitFishClass> Fishes { get; private set; } = new();
    public static List<BaitFishClass> LureFishes => Fishes.Where(f => f.LureMessage != "").ToList();

    public static List<ImportedFish> ImportedFishes { get; private set; } = new();

    public static List<BiteTimers> BiteTimers { get; private set; } = new();

    public static void Initialize()
    {
        Baits = [.. GenericHelpers.FindRows<Item>(i => i.ItemSearchCategory.RowId == FishingTackleRow).ToList()
            .Concat(GenericHelpers.FindRows<WKSItemInfo>(i => i.WKSItemSubCategory.RowId == 5).Select(i => i.Item.Value).ToList())
            .Select(b => new BaitFishClass(b))];

        Fishes = GenericHelpers.FindRows<FishParameter>(f => f.Item.RowId != 0 && f.Item.RowId < 1000000)
            .Select(f => new BaitFishClass(f)).GroupBy(f => f.Id).Select(group => group.First()).ToList() ?? [];

        try
        {
            var fishList = Path.Combine(Service.PluginInterface.AssemblyLocation.DirectoryName!,
                $"Data\\FishData\\fish_list.json");

            if (File.Exists(fishList))
            {
                var json = File.ReadAllText(fishList);

                ImportedFishes = JsonSerializer.Deserialize<List<ImportedFish>>(json)!;
            }

            var biteTimers = Path.Combine(Service.PluginInterface.AssemblyLocation.DirectoryName!,
                $"Data\\FishData\\bitetimers.json");

            if (File.Exists(biteTimers))
            {
                var json = File.ReadAllText(biteTimers);

                BiteTimers = JsonSerializer.Deserialize<List<BiteTimers>>(json)!;
            }
        }
        catch (Exception e)
        {
            ImGui.SetClipboardText(e.Message);
            Service.PluginLog.Error($"{e.Message}");
        }
    }
}