using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Event;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.Game.WKS;

namespace AutoHook.SeFunctions;

public unsafe class BaitManager
{
    public bool IsValid => FishingMan != null;

    internal FishingEventHandler* FishingMan
    {
        get
        {
            var ef = EventFramework.Instance();
            if (ef == null) return null;
            var handler = ef->EventHandlerModule.FishingEventHandler;
            if (handler == null) return null;
            return handler;
        }
    }

    public FishingState FishingState => FishingMan is var fm && fm != null ? fm->State : FishingState.None;

    public uint? CurrentSwimBait
    {
        get
        {
            if (FishingMan == null) return null;
            if (FishingMan->CurrentSelectedSwimBait == -1) return null;
            if (FishingMan->CurrentSelectedSwimBait is < 0 or >= 3) return null;
            return FishingMan->SwimBaitItemIds[FishingMan->CurrentSelectedSwimBait];
        }
    }

    public Span<uint> SwimbaitIds
    {
        get
        {
            if (FishingMan == null)
                return [];
            try
            {
                return FishingMan->SwimBaitItemIds;
            }
            catch
            {
                return [];
            }
        }
    }

    public uint CurrentBaitSwimBait => CurrentSwimBait ?? Current;

    public uint Current
    {
        get
        {
            try
            {
                if (Player.Territory is { Value.TerritoryIntendedUse.RowId: 60 })
                {
                    var cosmicManager = WKSManager.Instance();
                    if (cosmicManager != null)
                        return cosmicManager->FishingBait;
                }

                var playerState = PlayerState.Instance();
                if (playerState != null)
                    return playerState->FishingBait;
            }
            catch
            {
                // Game state not ready
            }
            return 0;
        }
    }

    public ChangeBaitReturn ChangeBait(uint baitId)
    {
        if (baitId == Current)
            return ChangeBaitReturn.AlreadyEquipped;

        if (baitId == 0 || GameRes.Baits.All(b => b.Id != baitId))
            return ChangeBaitReturn.InvalidBait;

        if (PlayerRes.HasItem(baitId) <= 0)
            return ChangeBaitReturn.NotInInventory;

        return GameMain.ExecuteCommand(701, 4, (int)baitId, 0, 0) ? ChangeBaitReturn.Success : ChangeBaitReturn.UnknownError;
    }

    public ChangeBaitReturn ChangeSwimbait(uint index)
    {
        if (index > 2)
            return ChangeBaitReturn.InvalidBait;

        return GameMain.ExecuteCommand(701, 25, (int)index, 0, 0) ? ChangeBaitReturn.Success : ChangeBaitReturn.UnknownError;
    }

    public ChangeBaitReturn ChangeBait(BaitFishClass bait)
    {
        if (bait.Id == Current)
        {
            Service.PrintChat($"Bait \"{bait.Name}\" is already equipped.");
            return ChangeBaitReturn.AlreadyEquipped;
        }

        if (bait.Id == 0 || GameRes.Baits.All(b => b.Id != bait.Id))
        {
            Service.PrintChat($"Bait \"{bait.Name}\" is not a valid bait.");
            return ChangeBaitReturn.InvalidBait;
        }

        if (PlayerRes.HasItem((uint)bait.Id) <= 0)
        {
            Service.PrintChat($"Bait \"{bait.Name}\" is not in your inventory.");
            return ChangeBaitReturn.NotInInventory;
        }

        return GameMain.ExecuteCommand(701, 4, bait.Id, 0, 0) ? ChangeBaitReturn.Success : ChangeBaitReturn.UnknownError;
    }

    public int GetSwimbaitCount()
    {
        if (FishingMan == null) return 0;
        return FishingMan->SwimBaitItemIds.ToArray().Count(id => id != 0);
    }

    public int GetSwimbaitCountForFish(uint fishId)
    {
        if (FishingMan == null) return 0;
        return FishingMan->SwimBaitItemIds.ToArray().Count(id => id == fishId);
    }

    public bool IsSwimbaitFull() => GetSwimbaitCount() >= 3;
    public bool IsSwimbaitEmpty() => GetSwimbaitCount() == 0;

    /// <summary>
    /// Checks if the current bait on the line is a moochable fish (swimbait case).
    /// For normal mooching, Current stays as the original bait, so this will return false.
    /// Use the isMooching parameter in GetCurrentBaitMoochId to handle normal mooching.
    /// </summary>
    public bool IsMooching() => GameRes.MoochableFish.Any(f => f.Id == Current);

    /// <summary>
    /// Gets the current bait/mooch ID on the line. Returns the fish ID if mooching/swimbait, otherwise returns the bait ID.
    /// </summary>
    /// <param name="fallbackId">Optional fallback ID (last catch) to use only when actually mooching</param>
    /// <param name="isMooching">If actually mooching (mooch action was used)</param>
    /// <returns>The current bait or mooch fish ID</returns>
    public int GetCurrentBaitMoochId(int? fallbackId = null, bool isMooching = false)
    {
        var currentId = Current;

        if (GameRes.Fishes.Any(f => f.Id == currentId))
            return (int)currentId;

        if (isMooching && fallbackId.HasValue && fallbackId.Value > 0 && GameRes.Fishes.Any(f => f.Id == fallbackId.Value))
            return fallbackId.Value;

        return (int)currentId;
    }

    public enum ChangeBaitReturn
    {
        Success,
        AlreadyEquipped,
        NotInInventory,
        InvalidBait,
        UnknownError,
    }
}
