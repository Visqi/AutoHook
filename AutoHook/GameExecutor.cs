using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Task = System.Threading.Tasks.Task;

namespace AutoHook;

/// <summary>
/// Single place for executing fishing actions: cast, use item, change bait/swimbait.
/// Reads BlockCasting and action availability from <see cref="WorldState"/>.
/// </summary>
public sealed class GameExecutor(WorldState ws) {
    private bool _blockActionNoDelay;

    public bool IsCastAvailable() => ws.IsCastAvailable();

    public unsafe bool CastAction(uint id)
        => ActionManager.Instance()->UseAction(ActionType.Action, id);

    public unsafe void UseItems(uint id)
        => AgentInventoryContext.Instance()->UseItem(id);

    public void CastActionDelayed(uint actionId, ActionType actionType = ActionType.Action, string actionName = "") {
        if (ws.BlockCasting) return;

        if (actionType is ActionType.Action or ActionType.EventAction) {
            if (!ws.ActionAvailable(actionId, actionType)) return;
            ws.Execute(new WorldState.OpSetBlockCasting(true));
            Service.PrintDebug($"[Executor] Casting Action: {actionName}, Id: {actionId}");
            try { CastAction(actionId); }
            catch (Exception e) { Service.PrintDebug($"[Executor] Error casting: {actionName}, Id: {actionId}, {e}"); }
            DelayNextCast(actionId);
        }
        else if (actionType == ActionType.Item) {
            ws.Execute(new WorldState.OpSetBlockCasting(true));
            Service.PrintDebug($"[Executor] Using Item: {actionName}, Id: {actionId}");
            try { UseItems(actionId); }
            catch (Exception e) { Service.PrintDebug($"[Executor] Error using item: {actionName}, Id: {actionId}, {e}"); }
            DelayNextCast(actionId);
        }
    }

    public void CastActionNoDelay(uint actionId, ActionType actionType = ActionType.Action, string actionName = "") {
        if (_blockActionNoDelay) return;
        _blockActionNoDelay = true;
        if (actionType == ActionType.Action && ws.ActionAvailable(actionId, actionType)) {
            var casted = CastAction(actionId);
            if (casted) Service.PrintDebug($"[Executor] Casting Action: {actionName}, Id: {actionId}");
        }
        else if (actionType == ActionType.Item) {
            Service.PrintDebug($"[Executor] Using Item: {actionName}, Id: {actionId}");
            UseItems(actionId);
        }
        _blockActionNoDelay = false;
    }

    public async void DelayNextCast(uint actionId) {
        var delay = 0;
        try {
            delay = new Random().Next(Service.Configuration.DelayBetweenCastsMin, Service.Configuration.DelayBetweenCastsMax);
        }
        catch (Exception e) {
            Svc.Log.Error($"[Executor] Error getting delay: {e}");
        }
        await Task.Delay(delay + ConditionalDelay(actionId));
        ws.Execute(new WorldState.OpSetBlockCasting(false));
    }

    private static int ConditionalDelay(uint id) => id switch {
        IDs.Actions.ThaliaksFavor => 1100,
        IDs.Actions.MakeshiftBait => 1100,
        IDs.Actions.NaturesBounty => 1100,
        IDs.Item.Cordial => 1100,
        IDs.Item.HQCordial => 1100,
        IDs.Item.HiCordial => 1100,
        IDs.Item.WateredCordial => 1100,
        IDs.Item.HQWateredCordial => 1100,
        _ => 0,
    };

    public ChangeBaitReturn ChangeBait(uint baitId) {
        if (baitId == ws.Fishing.BaitInfo.BaitId) return ChangeBaitReturn.AlreadyEquipped;
        if (baitId == 0 || GameRes.Baits.All(b => b.Id != baitId)) return ChangeBaitReturn.InvalidBait;
        if (ws.GetItemCount(baitId) <= 0) return ChangeBaitReturn.NotInInventory;
        return GameMain.ExecuteCommand(701, 4, (int)baitId, 0, 0) ? ChangeBaitReturn.Success : ChangeBaitReturn.UnknownError;
    }

    public ChangeBaitReturn ChangeSwimbait(uint index) {
        if (index > 2) return ChangeBaitReturn.InvalidBait;
        return GameMain.ExecuteCommand(701, 25, (int)index, 0, 0) ? ChangeBaitReturn.Success : ChangeBaitReturn.UnknownError;
    }

    public ChangeBaitReturn ChangeBait(BaitFishClass bait) {
        if (bait.Id == ws.Fishing.BaitInfo.BaitId) {
            Service.PrintChat($"Bait \"{bait.Name}\" is already equipped.");
            return ChangeBaitReturn.AlreadyEquipped;
        }
        if (bait.Id == 0 || GameRes.Baits.All(b => b.Id != bait.Id)) {
            Service.PrintChat($"Bait \"{bait.Name}\" is not a valid bait.");
            return ChangeBaitReturn.InvalidBait;
        }
        if (ws.GetItemCount((uint)bait.Id) <= 0) {
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
