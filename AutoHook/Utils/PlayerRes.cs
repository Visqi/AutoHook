using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Task = System.Threading.Tasks.Task;

namespace AutoHook.Utils;

/// <summary>
/// Cast, use item, and delay helpers only. Uses <see cref="Service.WorldState"/> for block-casting only.
/// </summary>
public static class PlayerRes {
    private static WorldState WS => Service.WorldState;

    public static unsafe bool IsInActiveSpectralCurrent() {
        if (FFXIVClientStructs.FFXIV.Client.Game.Event.EventFramework.Instance()->GetInstanceContentOceanFishing() is null)
            return false;
        return FFXIVClientStructs.FFXIV.Client.Game.Event.EventFramework.Instance()->GetInstanceContentOceanFishing()->SpectralCurrentActive;
    }

    public static unsafe uint ActionStatus(uint id, ActionType actionType = ActionType.Action)
        => ActionManager.Instance()->GetActionStatus(actionType, id);

    public static unsafe bool CastAction(uint id)
        => ActionManager.Instance()->UseAction(ActionType.Action, id);

    public static unsafe int GetRecastGroups(uint id, ActionType actionType = ActionType.Action)
        => ActionManager.Instance()->GetRecastGroup((int)actionType, id);

    public static unsafe void UseItems(uint id)
        => AgentInventoryContext.Instance()->UseItem(id);

    public static unsafe uint CastActionCost(uint id, ActionType actionType = ActionType.Action)
        => (uint)ActionManager.GetActionCost(actionType, id, 0, 0, 0, 0);

    public static unsafe bool ActionOnCoolDown(uint id, ActionType actionType = ActionType.Action) {
        var group = GetRecastGroups(id, actionType);
        if (group == -1) return false;
        var recastDetail = ActionManager.Instance()->GetRecastGroupDetail(group);
        return recastDetail->Total - recastDetail->Elapsed > 0;
    }

    public static unsafe float GetCooldown(uint id, ActionType actionType) {
        var group = GetRecastGroups(id, actionType);
        if (group == -1) return 0;
        var recast = ActionManager.Instance()->GetRecastGroupDetail(group);
        return recast->Total - recast->Elapsed;
    }

    public static void CastActionDelayed(uint actionId, ActionType actionType = ActionType.Action, string actionName = "") {
        if (WS.BlockCasting) return;

        if (actionType is ActionType.Action or ActionType.EventAction) {
            if (!WS.ActionAvailable(actionId, actionType)) return;
            WS.Execute(new WorldState.OpSetBlockCasting(true));
            Service.PrintDebug(@$"[PlayerResources] Casting Action: {actionName}, Id: {actionId}");
            try { CastAction(actionId); }
            catch (Exception e) { Service.PrintDebug(@$"Error casting action: {actionName}, Id: {actionId}, {e}"); }
            DelayNextCast(actionId);
        }
        else if (actionType == ActionType.Item) {
            WS.Execute(new WorldState.OpSetBlockCasting(true));
            Service.PrintDebug(@$"[PlayerResources] Using Item: {actionName}, Id: {actionId}");
            try { UseItems(actionId); }
            catch (Exception e) { Service.PrintDebug(@$"Error casting action: {actionName}, Id: {actionId}, {e}"); }
            DelayNextCast(actionId);
        }
    }

    private static bool _blockActionNoDelay;

    public static void CastActionNoDelay(uint actionId, ActionType actionType = ActionType.Action, string actionName = "") {
        if (_blockActionNoDelay) return;
        _blockActionNoDelay = true;
        if (actionType == ActionType.Action && WS.ActionAvailable(actionId, actionType)) {
            var casted = CastAction(actionId);
            if (casted) Service.PrintDebug(@$"[PlayerResources] Casting Action: {actionName}, Id: {actionId}");
        }
        else if (actionType == ActionType.Item) {
            Service.PrintDebug(@$"[PlayerResources] Using Item: {actionName}, Id: {actionId}");
            UseItems(actionId);
        }
        _blockActionNoDelay = false;
    }

    public static async void DelayNextCast(uint actionId) {
        var delay = 0;
        try { delay = new Random().Next(Service.Configuration.DelayBetweenCastsMin, Service.Configuration.DelayBetweenCastsMax); }
        catch (Exception e) { Svc.Log.Error(@$"Error getting delay between casts: {e}"); }
        await Task.Delay(delay + ConditionalDelay(actionId));
        WS.Execute(new WorldState.OpSetBlockCasting(false));
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
}
