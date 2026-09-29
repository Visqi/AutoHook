using FFXIVClientStructs.FFXIV.Client.UI;

namespace AutoHook.World.UpdateModules;

public sealed class SpearfishingUpdateModule : IWorldUpdateModule {
    private readonly Dictionary<uint, int> _spearItemCountScratch = [];
    private readonly Dictionary<uint, int> _spearItemBaseline = [];
    private bool _spearItemCountsReady;

    public unsafe void Update(WorldState ws) {
        var windowOpen = false;
        var wariness = 0;
        var warinessMax = 0;
        AddonSpearFishing.FishInfo lane0 = default;
        AddonSpearFishing.FishInfo lane1 = default;
        AddonSpearFishing.FishInfo lane2 = default;
        SpearLaneLayout laneLayout = default;
        SpearFishLayout fishLayout0 = default;
        SpearFishLayout fishLayout1 = default;
        SpearFishLayout fishLayout2 = default;

        if (IGameGui.Get().TryGetAddon<AddonSpearFishing>("SpearFishing", out var addon)
            && addon != null
            && addon->AtkUnitBase.WindowNode != null) {
            windowOpen = true;
            var gauge = addon->GaugeBar;
            if (gauge != null) {
                wariness = gauge->Values[0].ValueInt;
                warinessMax = gauge->MaxValue;
            }

            lane0 = addon->Fish[0];
            lane1 = addon->Fish[1];
            lane2 = addon->Fish[2];

            var uiScale = addon->AtkUnitBase.Scale;
            var fishLines = addon->GetNodeById(43);
            if (fishLines != null) {
                laneLayout = new SpearLaneLayout(fishLines->X, fishLines->Y, fishLines->Width, fishLines->Height, fishLines->ScaleX, uiScale);
            }

            fishLayout0 = ReadFishLayout(addon, lane0, 61);
            fishLayout1 = ReadFishLayout(addon, lane1, 60);
            fishLayout2 = ReadFishLayout(addon, lane2, 59);
        }

        var sf = ws.Spearfishing;
        var wasWindowOpen = sf.WindowOpen;
        if (sf.WindowOpen != windowOpen || sf.Wariness != wariness || sf.WarinessMax != warinessMax)
            ws.Execute(new SpearfishingState.OpHud(windowOpen, wariness, warinessMax));

        if (windowOpen && !sf.SessionActive)
            ws.Execute(new SpearfishingState.OpSessionActive(true));
        else if (wasWindowOpen && !windowOpen && sf.SessionActive)
            ws.Execute(new SpearfishingState.OpEndSession());

        if (windowOpen) {
            var spot = ResolveCurrentSpearfishingSpot();
            if (!spot.IsEmpty && spot != sf.Spot)
                ws.Execute(new SpearfishingState.OpSpot(spot));
        }

        if (!FishLayoutEquals(fishLayout0, sf.FishLayout0) || !FishLayoutEquals(fishLayout1, sf.FishLayout1) || !FishLayoutEquals(fishLayout2, sf.FishLayout2) || !LaneLayoutEquals(laneLayout, sf.LaneLayout)) {
            ws.Execute(new SpearfishingState.OpFishLayout(laneLayout, fishLayout0, fishLayout1, fishLayout2));
        }
    }

    public void ProcessCatches(WorldState ws) {
        if (FishBaitCatalog.Get().SpearfishItemIds.Count == 0)
            return;

        _spearItemCountScratch.Clear();
        foreach (var itemId in FishBaitCatalog.Get().SpearfishItemIds) {
            var count = ws.Player.GetItemCount(itemId);
            if (count > 0)
                _spearItemCountScratch[itemId] = count;
        }

        if (!_spearItemCountsReady) {
            CommitSpearItemBaseline();
            return;
        }

        if (!ws.Spearfishing.SessionActive) {
            CommitSpearItemBaseline();
            return;
        }

        foreach (var (itemId, count) in _spearItemCountScratch) {
            var prev = _spearItemBaseline.GetValueOrDefault(itemId);
            if (count > prev) {
                var gained = count - prev;
                while (gained > 0) {
                    var chunk = (byte)Math.Min(gained, byte.MaxValue);
                    ws.Execute(new SpearfishingState.OpAddFishCaught(itemId, chunk));
                    gained -= chunk;
                }
            }
        }

        CommitSpearItemBaseline();
    }

    private void CommitSpearItemBaseline() {
        _spearItemBaseline.Clear();
        foreach (var (itemId, count) in _spearItemCountScratch)
            _spearItemBaseline[itemId] = count;
        _spearItemCountsReady = true;
    }

    private static unsafe SpearFishLayout ReadFishLayout(AddonSpearFishing* addon, AddonSpearFishing.FishInfo fish, uint nodeId) {
        var node = addon->GetNodeById(nodeId);
        if (node == null)
            return new SpearFishLayout(fish, 0, 0, 0);
        return new SpearFishLayout(fish, node->X, node->Width, node->ScaleX);
    }

    private static bool FishInfoEquals(AddonSpearFishing.FishInfo a, AddonSpearFishing.FishInfo b)
        => a.Available == b.Available && a.InverseDirection == b.InverseDirection && a.GuaranteedLarge == b.GuaranteedLarge && a.Size == b.Size && a.Speed == b.Speed;

    private static bool FishLayoutEquals(SpearFishLayout a, SpearFishLayout b)
        => FishInfoEquals(a.Fish, b.Fish)
           && Math.Abs(a.FishNodeX - b.FishNodeX) < 0.01f
           && Math.Abs(a.FishNodeWidth - b.FishNodeWidth) < 0.01f
           && Math.Abs(a.FishNodeScaleX - b.FishNodeScaleX) < 0.01f;

    private static bool LaneLayoutEquals(SpearLaneLayout a, SpearLaneLayout b)
        => Math.Abs(a.LaneX - b.LaneX) < 0.01f
           && Math.Abs(a.LaneY - b.LaneY) < 0.01f
           && Math.Abs(a.LaneWidth - b.LaneWidth) < 0.01f
           && Math.Abs(a.LaneHeight - b.LaneHeight) < 0.01f
           && Math.Abs(a.LaneScaleX - b.LaneScaleX) < 0.01f
           && Math.Abs(a.UiScale - b.UiScale) < 0.01f;

    private static SpearfishingSpotState ResolveCurrentSpearfishingSpot() {
        if (ITargetManager.Get().Target is not { ObjectKind: Dalamud.Game.ClientState.Objects.Enums.ObjectKind.GatheringPoint, BaseId: var pointId })
            return SpearfishingSpotState.Empty;

        if (!FishSpotCatalog.Get().TryGetSpot(pointId, out var spot))
            return new SpearfishingSpotState(pointId, 0, 0, false);

        return new SpearfishingSpotState(spot.GatheringPointId, spot.GatheringPointBaseId, spot.NotebookId, spot.IsShadowNode);
    }
}
