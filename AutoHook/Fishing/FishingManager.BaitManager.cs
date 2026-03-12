using FFXIVClientStructs.FFXIV.Client.Game;

namespace AutoHook.Fishing;

public partial class FishingManager {
    public static ChangeBaitReturn ChangeBait(uint baitId) {
        if (baitId == Ws.Fishing.BaitInfo.BaitId) return ChangeBaitReturn.AlreadyEquipped;
        if (baitId == 0 || GameRes.Baits.All(b => b.Id != baitId)) return ChangeBaitReturn.InvalidBait;
        if (Ws.GetItemCount(baitId) <= 0) return ChangeBaitReturn.NotInInventory;
        return GameMain.ExecuteCommand(701, 4, (int)baitId, 0, 0) ? ChangeBaitReturn.Success : ChangeBaitReturn.UnknownError;
    }

    public static ChangeBaitReturn ChangeSwimbait(uint index) {
        if (index > 2) return ChangeBaitReturn.InvalidBait;
        return GameMain.ExecuteCommand(701, 25, (int)index, 0, 0) ? ChangeBaitReturn.Success : ChangeBaitReturn.UnknownError;
    }

    public static ChangeBaitReturn ChangeBait(BaitFishClass bait) {
        if (bait.Id == Ws.Fishing.BaitInfo.BaitId) {
            Service.PrintChat($"Bait \"{bait.Name}\" is already equipped.");
            return ChangeBaitReturn.AlreadyEquipped;
        }
        if (bait.Id == 0 || GameRes.Baits.All(b => b.Id != bait.Id)) {
            Service.PrintChat($"Bait \"{bait.Name}\" is not a valid bait.");
            return ChangeBaitReturn.InvalidBait;
        }
        if (Ws.GetItemCount((uint)bait.Id) <= 0) {
            Service.PrintChat($"Bait \"{bait.Name}\" is not in your inventory.");
            return ChangeBaitReturn.NotInInventory;
        }
        return GameMain.ExecuteCommand(701, 4, bait.Id, 0, 0) ? ChangeBaitReturn.Success : ChangeBaitReturn.UnknownError;
    }

    public enum ChangeBaitReturn {
        Success,
        AlreadyEquipped,
        NotInInventory,
        InvalidBait,
        UnknownError,
    }
}
