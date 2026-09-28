using FFXIVClientStructs.FFXIV.Client.Game;

namespace AutoHook.Modules.Rod;

public sealed class BaitComponent(RodFishingModule module) : RodComponent(module) {
    public static ChangeBaitReturn ChangeBait(uint baitId) {
        var ws = Service.WorldState;
        if (baitId == ws.Fishing.BaitInfo.BaitId) return ChangeBaitReturn.AlreadyEquipped;
        if (baitId == 0 || GameRes.Baits.All(b => b.Id != baitId)) return ChangeBaitReturn.InvalidBait;
        if (ws.Player.GetItemCount(baitId) <= 0) return ChangeBaitReturn.NotInInventory;
        return GameMain.ExecuteCommand(701, 4, (int)baitId, 0, 0) ? ChangeBaitReturn.Success : ChangeBaitReturn.UnknownError;
    }

    public static ChangeBaitReturn ChangeSwimbait(uint index) {
        if (index > 2) return ChangeBaitReturn.InvalidBait;
        return GameMain.ExecuteCommand(701, 25, (int)index, 0, 0) ? ChangeBaitReturn.Success : ChangeBaitReturn.UnknownError;
    }

    public static ChangeBaitReturn ChangeBait(BaitFishClass bait) {
        var ws = Service.WorldState;
        if (bait.Id == ws.Fishing.BaitInfo.BaitId) {
            Service.PrintChat($"Bait \"{bait.Name}\" is already equipped.");
            return ChangeBaitReturn.AlreadyEquipped;
        }
        if (bait.Id == 0 || GameRes.Baits.All(b => b.Id != bait.Id)) {
            Service.PrintChat($"Bait \"{bait.Name}\" is not a valid bait.");
            return ChangeBaitReturn.InvalidBait;
        }
        if (ws.Player.GetItemCount((uint)bait.Id) <= 0) {
            Service.PrintChat($"Bait \"{bait.Name}\" is not in your inventory.");
            return ChangeBaitReturn.NotInInventory;
        }
        return GameMain.ExecuteCommand(701, 4, bait.Id, 0, 0) ? ChangeBaitReturn.Success : ChangeBaitReturn.UnknownError;
    }
}
