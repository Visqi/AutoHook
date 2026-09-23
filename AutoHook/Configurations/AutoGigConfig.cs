using AutoHook.Spearfishing;
using AutoHook.Spearfishing.Enums;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using Newtonsoft.Json;
using System.ComponentModel;
using System.Runtime.Serialization;

namespace AutoHook.Configurations;

public class AutoGigConfig : BasePresetConfig {
    [DefaultValue("Old Preset")]
    public string Name { get; set; } = "Old Preset";

    public List<BaseGig> Gigs { get; set; } = [];

    [DefaultValue(25)]
    public int HitboxSize = 25;

    public AutoCollect Collect { get; set; } = new(true);
    public AutoThaliaksFavor ThaliaksFavor { get; set; } = new(true);
    public AutoCordial Cordial { get; set; } = new(true);
    public bool RetainCountersBetweenSessions;

    [JsonIgnore] public uint SelectedAddPoolId;

    public AutoGigConfig(string presetName) => PresetName = presetName;

    [OnDeserialized]
    private void OnDeserialized(StreamingContext _) => PrepareActions();

    public void PrepareActions() {
        Collect.IsSpearFishing = true;
        ThaliaksFavor.IsSpearFishing = true;
        Cordial.IsSpearFishing = true;
    }

    public List<BaseGig> GetGigsForPool(uint spearfishingNotebookId) {
        var candidates = Gigs.Where(gig => gig.Fish != null);
        if (spearfishingNotebookId == 0)
            return [.. candidates.Where(gig => gig.IsAnyPool)];

        return
        [
            .. candidates.Where(gig => gig.SpearfishingNotebookId == spearfishingNotebookId),
            .. candidates.Where(gig => gig.IsAnyPool),
        ];
    }

    public BaseGig? FindGigForPool(
        uint spearfishingNotebookId,
        SpearfishSpeed speed,
        SpearfishSize size) {
        return GetGigsForPool(spearfishingNotebookId)
            .FirstOrDefault(gig => gig.Speed == speed && gig.Size == size);
    }

    public List<BaseGig> GetGigCurrentNode(int node) {
        var notebookId = node > 0 && GameRes.SpearfishingSpotsByPointId.TryGetValue((uint)node, out var spot) ? spot.NotebookId : 0;
        return GetGigsForPool(notebookId);
    }

    public void RegenerateNestedUniqueIds() {
        foreach (var gig in Gigs)
            gig.RegenerateUniqueId();
    }

    public void ResetCounter() {
        SpearfishingCounterHelper.Reset(Gigs);
        if (Service.WorldState.Spearfishing.FishCaughtCounts.Count > 0)
            Service.WorldState.Execute(new SpearfishingInfo.OpResetFishCaught());
    }

    public override void AddItem(BaseOption item) {
        Gigs.Add((BaseGig)item);
        Service.Save();
    }

    public override void RemoveItem(Guid value) {
        SpearfishingCounterHelper.Remove(value);
        Gigs.RemoveAll(x => x.UniqueId == value);
        Service.Save();
    }

    public override void DrawOptions() {
        if (Gigs.Count == 0)
            return;

        foreach (var group in Gigs.ToList().GroupBy(gig => gig.SpearfishingNotebookId).OrderBy(group => group.Key == 0 ? 0 : 1).ThenBy(group => GetPoolName(group.Key))) {
            using var poolId = ImRaii.PushId($"pool_{group.Key}");
            if (!ImGui.TreeNodeEx(GetPoolName(group.Key), ImGuiTreeNodeFlags.DefaultOpen))
                continue;

            if (ImGui.SmallIconButton(FontAwesomeIcon.Plus))
                AddItem(new BaseGig(0) { SpearfishingNotebookId = group.Key });
            DrawUtil.HoveredTooltip("Add fish");

            foreach (var gig in group)
                DrawGig(gig);
            ImGui.TreePop();
        }
    }

    private void DrawGig(BaseGig gig) {
        using var gigId = ImRaii.PushId(gig.UniqueId.ToString());
        using (ImRaii.PushFont(UiBuilder.IconFont)) {
            var icon = FontAwesomeIcon.Trash.ToIconString();
            var buttonSize = ImGui.CalcTextSize(icon) + ImGui.GetStyle().FramePadding * 2;
            if (ImGui.Button(@$"{icon}", buttonSize) &&
                ImGui.GetIO().KeyShift) {
                RemoveItem(gig.UniqueId);
                Service.Save();
                return;
            }
        }

        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(UIStrings.HoldShiftToDelete);

        ImGui.SameLine(0, 3.Scaled());

        DrawUtil.Checkbox(@$"", ref gig.Enabled);

        ImGui.SameLine(0, 3.Scaled());

        var x = ImGui.GetCursorPosX();
        if (ImGui.TreeNodeEx($"{gig.Fish?.Name ?? UIStrings.None}", ImGuiTreeNodeFlags.FramePadding)) {
            ImGui.SetCursorPosX(x);
            using (ImRaii.Group()) {
                gig.DrawOptions();
                ImGui.TextDisabled($"Caught: {SpearfishingCounterHelper.GetFishCount(gig.UniqueId)}");
            }
            ImGui.TreePop();
        }
    }

    public static string GetPoolName(uint notebookId)
        => notebookId == 0 ? "Any Pool" : GameRes.SpearfishingPoolsByNotebookId.TryGetValue(notebookId, out var pool) ? pool.Name : $"Unknown Pool #{notebookId}";
}
