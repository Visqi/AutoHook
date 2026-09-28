using Dalamud.Game.Inventory.InventoryEventArgTypes;
using FFXIVClientStructs.FFXIV.Client.Game.Event;
using FFXIVClientStructs.FFXIV.Client.System.Framework;

namespace AutoHook.World;

public sealed class WorldStateUpdater : IPluginService, IAsyncDisposable {
    private readonly DateTime _startTime = DateTime.UtcNow;
    private readonly long _startQpc;

    private readonly PlayerUpdateModule _player = new();
    private readonly PartyUpdateModule _party = new();
    private readonly OceanUpdateModule _ocean = new();
    private readonly WksUpdateModule _wks = new();
    private readonly FishingUpdateModule _fishing = new();
    private readonly SpearfishingUpdateModule _spear = new();
    private readonly HooksUpdateModule _hooks;

    public bool HasPendingGp => _hooks.HasPendingGp;

    public unsafe WorldStateUpdater() {
        _startQpc = Framework.Instance()->PerformanceCounterValue;
        _hooks = new HooksUpdateModule(_player.MarkInventoryDirty);
        IGameInventory.Get().InventoryChanged += OnInventoryChanged;
    }

    public async ValueTask DisposeAsync() {
        await _hooks.DisposeAsync();
        IGameInventory.Get().InventoryChanged -= OnInventoryChanged;
    }

    // push current game state into WorldState. call every frame.
    public unsafe void Update() {
        if (Player.ClassJob.RowId is not 18 || IObjectTable.Get().LocalPlayer is not { } lp)
            return;

        var ws = WorldState.Get();
        var fwk = Framework.Instance();
        if (fwk == null)
            return;

        ws.Execute(new WorldState.OpFrameStart(new FrameState(
            _startTime.AddSeconds((double)(fwk->PerformanceCounterValue - _startQpc) / ws.QPF),
            (ulong)fwk->PerformanceCounterValue,
            fwk->FrameCounter,
            fwk->RealFrameDeltaTime,
            fwk->FrameDeltaTime,
            fwk->GameSpeedMultiplier)));

        var gp = lp.CurrentGp;
        var maxGp = lp.MaxGp;
        if (ws.Player.CurrentGp != gp || ws.Player.MaxGp != maxGp)
            ws.Execute(new PlayerState.OpGp(gp, maxGp));

        var level = lp.Level;
        if (ws.Player.Level != level)
            ws.Execute(new PlayerState.OpLevel(level));

        _player.Update(ws);
        _party.Update(ws);
        _ocean.Update(ws);
        _wks.Update(ws);

        var PreviousFishingState = ws.Fishing.FishingState;
        _fishing.Update(ws);

        if (PreviousFishingState == FishingState.None && ws.Fishing.FishingState != FishingState.None)
            ws.Execute(new WorldState.OpBeganSession());
        else if (PreviousFishingState != FishingState.None && ws.Fishing.FishingState == FishingState.None)
            ws.Execute(new WorldState.OpEndedSession());

        if (_player.ConsumeInventoryDirty()) {
            _player.CollectAndApplyInventory(ws);
            _spear.ProcessCatches(ws);
        }

        _spear.Update(ws);

        var fishingState = ws.Fishing.FishingState;
        if (ShouldCaptureCastSnapshot(PreviousFishingState, fishingState))
            ws.Execute(new RodState.OpUpdateCastSnapshot(PreviousFishingState));
        else if (ws.Fishing.CastSnapshot.Active && fishingState is not FishingState.LineInWater and not FishingState.Bite)
            ws.Execute(new RodState.OpInvalidateCastSnapshot());
    }

    private static bool ShouldCaptureCastSnapshot(FishingState previous, FishingState current)
        => previous != FishingState.LineInWater && current == FishingState.LineInWater;

    public void RefreshFishingStateSnapshot() {
        if (Player.ClassJob.RowId is not 18 || IObjectTable.Get().LocalPlayer is null)
            return;

        _fishing.Refresh(WorldState.Get());
    }

    private void OnInventoryChanged(IReadOnlyCollection<InventoryEventArgs> _)
        => _player.MarkInventoryDirty();
}
