using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;

namespace AutoHook.Actions;

public enum ActionDelayMode {
    Delayed,
    NoDelay,
}

public readonly record struct ActionRequest(uint Id, ActionType Type = ActionType.Action, string Name = "", ActionDelayMode DelayMode = ActionDelayMode.Delayed);

public sealed class ActionExecutor : IDisposable {
    private readonly WorldState _ws;
    private readonly Random _rng = new();
    private long _unblockAtTickMs;

    public ActionExecutor(WorldState worldState) {
        _ws = worldState;
        Svc.Framework.Update += OnFrameworkUpdate;
    }

    public void Dispose() {
        Svc.Framework.Update -= OnFrameworkUpdate;
        _unblockAtTickMs = 0;
        if (_ws.Player.BlockCasting)
            _ws.Execute(new WorldState.OpSetBlockCasting(false));
    }

    private void OnFrameworkUpdate(IFramework _) => Update();

    public void Update() {
        if (_unblockAtTickMs == 0 || Environment.TickCount64 < _unblockAtTickMs)
            return;

        _unblockAtTickMs = 0;
        if (_ws.Player.BlockCasting)
            _ws.Execute(new WorldState.OpSetBlockCasting(false));
    }

    public bool TryCast(ActionRequest request)
        => request.DelayMode == ActionDelayMode.NoDelay ? TryCastNoDelay(request.Id, request.Type, request.Name) : TryCastDelayed(request.Id, request.Type, request.Name);

    public bool TryCastDelayed(uint actionId, ActionType actionType = ActionType.Action, string actionName = "") {
        if (_ws.Player.BlockCasting)
            return false;

        if (actionType is not (ActionType.Action or ActionType.EventAction or ActionType.Item))
            return false;

        if (!_ws.ActionAvailable(actionId, actionType))
            return false;

        _ws.Execute(new WorldState.OpSetBlockCasting(true));
        try {
            UseAction(actionId, actionType);
        }
        catch (Exception e) {
            Svc.Log.Error(e, $"Error casting action: {actionName}, Id: {actionId}");
        }

        BeginPostCastDelay();
        return true;
    }

    public bool TryCastNoDelay(uint actionId, ActionType actionType = ActionType.Action, string actionName = "") {
        if (_ws.Player.BlockCasting)
            return false;

        if (!_ws.ActionAvailable(actionId, actionType))
            return false;

        try {
            return UseAction(actionId, actionType);
        }
        catch (Exception e) {
            Svc.Log.Error(e, $"Error casting action: {actionName}, Id: {actionId}");
            return false;
        }
    }

    public bool TryUseStellarHookset(string actionName = "Stellar Hookset")
        => _ws.GetAvailableStellarHooksetId() is { } actionId && TryCastDelayed(actionId, ActionType.Action, actionName);

    public unsafe bool UseAction(uint id, ActionType actionType = ActionType.Action) {
        if (actionType == ActionType.Item) {
            AgentInventoryContext.Instance()->UseItem(id);
            return true;
        }

        return ActionManager.Instance()->UseAction(actionType, id);
    }

    public void BeginPostCastDelay() {
        var delayMs = GetPostCastDelayMs();
        _unblockAtTickMs = Environment.TickCount64 + delayMs;
        if (!_ws.Player.BlockCasting)
            _ws.Execute(new WorldState.OpSetBlockCasting(true));
    }

    public int GetPostCastDelayMs() {
        try {
            var min = Service.Configuration.DelayBetweenCastsMin;
            var max = Service.Configuration.DelayBetweenCastsMax;
            if (max < min)
                (min, max) = (max, min);
            return _rng.Next(min, max + 1);
        }
        catch (Exception e) {
            Svc.Log.Error(@$"Error getting delay between casts: {e}");
            return 0;
        }
    }

    public static uint GetActionCost(uint id, ActionType actionType = ActionType.Action)
        => (uint)ActionManager.GetActionCost(actionType, id, 0, 0, 0, 0);
}
