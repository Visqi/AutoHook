using AutoHook.Conditions;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Event;

namespace AutoHook.SeFunctions;

/// <summary>
/// Bait/swimbait change commands only. Reads state from <see cref="Service.WorldState"/>.
/// </summary>
public unsafe class BaitManager
{
    private static WorldState WS => Service.WorldState;

    public bool IsValid => FishingMan != null;

    internal FishingEventHandler* FishingMan
    {
        get
        {
            var ef = EventFramework.Instance();
            if (ef == null) return null;
            return ef->EventHandlerModule.FishingEventHandler;
        }
    }

    public ChangeBaitReturn ChangeBait(uint baitId)
    {
        if (baitId == WS.CurrentBaitId) return ChangeBaitReturn.AlreadyEquipped;
        if (baitId == 0 || GameRes.Baits.All(b => b.Id != baitId)) return ChangeBaitReturn.InvalidBait;
        if (WS.GetItemCount(baitId) <= 0) return ChangeBaitReturn.NotInInventory;
        return GameMain.ExecuteCommand(701, 4, (int)baitId, 0, 0) ? ChangeBaitReturn.Success : ChangeBaitReturn.UnknownError;
    }

    public ChangeBaitReturn ChangeSwimbait(uint index)
    {
        if (index > 2) return ChangeBaitReturn.InvalidBait;
        return GameMain.ExecuteCommand(701, 25, (int)index, 0, 0) ? ChangeBaitReturn.Success : ChangeBaitReturn.UnknownError;
    }

    public ChangeBaitReturn ChangeBait(BaitFishClass bait)
    {
        if (bait.Id == WS.CurrentBaitId)
        {
            Service.PrintChat($"Bait \"{bait.Name}\" is already equipped.");
            return ChangeBaitReturn.AlreadyEquipped;
        }
        if (bait.Id == 0 || GameRes.Baits.All(b => b.Id != bait.Id))
        {
            Service.PrintChat($"Bait \"{bait.Name}\" is not a valid bait.");
            return ChangeBaitReturn.InvalidBait;
        }
        if (WS.GetItemCount((uint)bait.Id) <= 0)
        {
            Service.PrintChat($"Bait \"{bait.Name}\" is not in your inventory.");
            return ChangeBaitReturn.NotInInventory;
        }
        return GameMain.ExecuteCommand(701, 4, bait.Id, 0, 0) ? ChangeBaitReturn.Success : ChangeBaitReturn.UnknownError;
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
