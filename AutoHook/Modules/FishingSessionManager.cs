using AutoHook.Modules.Gig;
using AutoHook.Tasks;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Event;

namespace AutoHook.Modules;

public sealed class FishingSessionManager : IDisposable {
    private const uint FisherJobId = 18;

    public RodFishingModule Rod { get; }
    public GigFishingModule Gig { get; }

    private static WorldState Ws => Service.WorldState;

    public FishingSessionManager() {
        Rod = new RodFishingModule(Service.WorldState);
        Gig = new GigFishingModule(Service.WorldState);
        Svc.Framework.Update += OnFrameworkUpdate;
    }

    public void Dispose() {
        Svc.Framework.Update -= OnFrameworkUpdate;
        Rod.Dispose();
        Gig.Dispose();
    }

    public void StartFishing() => Rod.StartFishing();

    public void RequestStopAfterNextFish() => Rod.RequestStopAfterNextFish();

    private void OnFrameworkUpdate(IFramework _) {
        if (!Service.Configuration.PluginEnabled || !Svc.ClientState.IsLoggedIn || Svc.Objects.LocalPlayer == null) {
            if (!Service.Configuration.PluginEnabled && Svc.Automation.CurrentTask is AutoOceanFish)
                Svc.Automation.Stop();

            var sf = Ws.Spearfishing;
            if (sf.SessionActive || sf.WindowOpen || !sf.Spot.IsEmpty || sf.Wariness != 0)
                Ws.Execute(new SpearfishingInfo.OpEndSession());
            return;
        }

        Service.WorldStateUpdater.Update();

        if (Svc.Automation.CurrentTask is AutoOceanFish && Ws.Fishing.FishingState != FishingState.None) {
            Ws.Decide(DecisionContext.OceanPreset, true, "Stop task", "already fishing");
            Svc.Automation.Stop();
        }

        if (Player.ClassJob.RowId != FisherJobId) {
            Rod.ClearWorldState();
            return;
        }

        if (Ws.Spearfishing.WindowOpen || Ws.Spearfishing.SessionActive)
            Gig.Update();
        else
            Rod.Update();
    }
}
