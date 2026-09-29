using AutoHook.Spearfishing;

namespace AutoHook.Modules.Gig;

public sealed class GigFishingModule : FishingModule {
    public GigComponent Gig { get; }
    public GigAutoCastComponent AutoCast { get; }

    public ActionHints Hints { get; } = new();
    public ActionHintsResolver Resolver { get; } = new();

    public Guid LastGigEntryId { get; set; }

    private static SpearFishingPresets GigCfg => Configuration.C.AutoGigConfig;
    private WorldState Ws => WorldState;

    public GigFishingModule(WorldState worldState) : base(worldState) {
        Gig = AddComponent(new GigComponent(this));
        AutoCast = AddComponent(new GigAutoCastComponent(this));
        Ws.Modified += OnWorldStateModified;
    }

    public override void Dispose() {
        Ws.Modified -= OnWorldStateModified;
    }

    public bool Enqueue(ActionRequest request, bool forceQueue = false)
        => ActionExecutor.Get().Enqueue(request, forceQueue);

    public override void Update() {
        if (!Configuration.C.PluginEnabled || !GigCfg.AutoGigEnabled)
            return;

        if (!Ws.Spearfishing.WindowOpen && !Ws.Spearfishing.SessionActive)
            return;

        Gig.Update();

        Hints.Clear();
        AutoCast.ContributeHints(Hints);
        Resolver.Resolve(Ws, Hints);
    }

    private void OnWorldStateModified(WorldState.Operation op) {
        if (op is SpearfishingState.OpAddFishCaught caught) {
            OnFishCaught(caught);
        }
        else if (op is SpearfishingState.OpEndSession) {
            LastGigEntryId = Guid.Empty;
            if (GigCfg.SelectedPreset is not { RetainCountersBetweenSessions: true }) {
                SpearfishingCounterHelper.ResetAll();
                Ws.Execute(new SpearfishingState.OpResetFishCaught());
            }
        }
    }

    private void OnFishCaught(SpearfishingState.OpAddFishCaught caught) {
        var preset = GigCfg.SelectedPreset;
        AutoVeteranTrade? veteranTrade = null;

        if (preset != null) {
            var matched = LastGigEntryId == Guid.Empty ? null : preset.Gigs.FirstOrDefault(gig => gig.UniqueId == LastGigEntryId && gig.Fish?.ItemId == caught.FishId);
            matched ??= preset.GetGigsForPool(Ws.Spearfishing.Spot.NotebookId).FirstOrDefault(gig => gig.Fish?.ItemId == caught.FishId);
            if (matched != null)
                SpearfishingCounterHelper.AddFishCount(matched.UniqueId, caught.Amount);
            veteranTrade = matched?.VeteranTrade;
        }
        else {
            veteranTrade = GigCfg.CatchAllVeteranTradeAction;
        }

        LastGigEntryId = Guid.Empty;

        if (veteranTrade == null)
            return;

        Hints.Clear();
        AutoCast.ContributeVeteranTradeHints(Hints, veteranTrade);
        Resolver.Resolve(Ws, Hints);
    }
}
