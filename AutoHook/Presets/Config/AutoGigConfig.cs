using AutoHook.Spearfishing;
using AutoHook.Spearfishing.Enums;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility.Raii;
using Newtonsoft.Json;
using System.ComponentModel;
using System.Numerics;
using System.Runtime.Serialization;

namespace AutoHook.Presets.Config;

public class AutoGigConfig : BasePresetConfig {
    [DefaultValue("Old Preset")]
    public string Name { get; set; } = "Old Preset";

    public List<BaseGig> Gigs { get; set; } = [];

    [DefaultValue(25)]
    public int HitboxSize = 25;

    public AutoCollect Collect { get; set; } = new(true);
    public AutoThaliaksFavor ThaliaksFavor { get; set; } = new(true);
    public AutoCordial Cordial { get; set; } = new(true);
    public AutoBaitedBreath BaitedBreath { get; set; } = new(true);
    public AutoElectricCurrent ElectricCurrent { get; set; } = new(true);
    public AutoVitalSight VitalSight { get; set; } = new(true);
    public bool RetainCountersBetweenSessions;

    [JsonIgnore] public uint SelectedAddPoolId;

    public AutoGigConfig(string presetName) => PresetName = presetName;

    [OnDeserialized]
    private void OnDeserialized(StreamingContext _) => PrepareActions();

    public void PrepareActions() {
        Collect.IsSpearFishing = true;
        ThaliaksFavor.IsSpearFishing = true;
        Cordial.IsSpearFishing = true;
        BaitedBreath.IsSpearFishing = true;
        ElectricCurrent.IsSpearFishing = true;
        VitalSight.IsSpearFishing = true;
        foreach (var gig in Gigs) {
            gig.NaturesBounty.IsSpearFishing = true;
            gig.VeteranTrade.IsSpearFishing = true;
        }
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
        if (WorldState.Get().Spearfishing.FishCaughtCounts.Count > 0)
            WorldState.Get().Execute(new SpearfishingState.OpResetFishCaught());
    }

    public override void AddItem(BaseOption item) {
        Gigs.Add((BaseGig)item);
        Configuration.Save();
    }

    public override void RemoveItem(Guid value) {
        SpearfishingCounterHelper.Remove(value);
        Gigs.RemoveAll(x => x.UniqueId == value);
        Configuration.Save();
    }

    public override void DrawOptions() {
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ImGui.GetContentRegionAvail().X / 2 - ImGui.CalcTextSize(PresetName).X / 2);
        ImGui.TextColored(ImGuiColors.DalamudOrange, $" {PresetName}");

        using var tabs = ImRaii.TabBar("###GigPresetTabs", ImGuiTabBarFlags.NoTooltip);
        if (!tabs)
            return;

        using (var tab = ImRaii.TabItem(UIStrings.FishCaught)) {
            if (tab)
                DrawFishTab();
        }

        using (var tab = ImRaii.TabItem(UIStrings.Auto_Casts)) {
            if (tab)
                DrawActionsTab();
        }

        using (var tab = ImRaii.TabItem(UIStrings.ExtraOptions)) {
            if (tab)
                DrawExtraTab();
        }
    }

    private void DrawFishTab() {
        var poolIds = new List<uint> { 0 };
        poolIds.AddRange(GameRes.SpearfishingPoolsByNotebookId.Keys.Where(id => id != 0).OrderBy(GetPoolName));

        if (ImGui.Button(UIStrings.Add)) {
            if (!Gigs.Any(gig => gig.SpearfishingNotebookId == SelectedAddPoolId && gig.Fish == null))
                AddItem(new BaseGig(0) { SpearfishingNotebookId = SelectedAddPoolId });
            Configuration.Save();
        }

        ImGui.SameLine();
        ImGui.Text($"{UIStrings.Add_new_fish} ({Gigs.Count})");
        ImGui.SameLine();
        DrawUtil.DrawComboSelector(poolIds, GetPoolName, GetPoolName(SelectedAddPoolId), id => SelectedAddPoolId = id);

        using var item = ImRaii.Child("###GigFishItems", new Vector2(0, 0), false);
        DrawFishList();
    }

    private void DrawActionsTab() {
        var actionX = ImGui.GetCursorPosX();
        ImGui.SetCursorPosX(actionX);
        Collect.DrawConfig();
        ImGui.SetCursorPosX(actionX);
        ThaliaksFavor.DrawConfig();
        ImGui.SetCursorPosX(actionX);
        Cordial.DrawConfigWithLabel("Cordials");
        ImGui.SetCursorPosX(actionX);
        BaitedBreath.DrawConfig();
        ImGui.SetCursorPosX(actionX);
        VitalSight.DrawConfig();
        ImGui.SetCursorPosX(actionX);
        ElectricCurrent.DrawConfig();
    }

    private void DrawExtraTab() {
        ImGui.SetNextItemWidth(90.Scaled());
        if (ImGui.InputInt(UIStrings.GigHitbox, ref HitboxSize)) {
            HitboxSize = Math.Max(0, Math.Min(HitboxSize, 300));
            Configuration.Save();
        }

        DrawUtil.Checkbox("Retain counters between pools", ref RetainCountersBetweenSessions);
        if (ImGui.Button("Reset caught counters"))
            ResetCounter();
    }

    private void DrawFishList() {
        if (Gigs.Count == 0)
            return;

        foreach (var group in Gigs.ToList().GroupBy(gig => gig.SpearfishingNotebookId).OrderBy(group => group.Key == 0 ? 0 : 1).ThenBy(group => GetPoolName(group.Key))) {
            using var poolId = ImRaii.PushId($"pool_{group.Key}");
            var gigs = group.ToList();
            var poolEnabled = gigs.All(gig => gig.Enabled);
            var wasEnabled = poolEnabled;

            DrawUtil.DrawCheckboxHeader(GetPoolName(group.Key), ref poolEnabled, ImGuiTreeNodeFlags.DefaultOpen, () => {
                ImGui.Spacing();
                if (ImGui.Button(UIStrings.Add)) {
                    AddItem(new BaseGig(0) { SpearfishingNotebookId = group.Key });
                    Configuration.Save();
                }

                ImGui.SameLine();
                ImGui.Text($"{UIStrings.Add_new_fish} ({gigs.Count})");
                ImGui.Spacing();

                foreach (var gig in gigs)
                    DrawGig(gig);
            });

            if (poolEnabled != wasEnabled) {
                foreach (var gig in gigs)
                    gig.Enabled = poolEnabled;
                Configuration.Save();
            }

            ImGui.Spacing();
        }
    }

    private void DrawGig(BaseGig gig) {
        using var gigId = ImRaii.PushId(gig.UniqueId.ToString());

        var count = SpearfishingCounterHelper.GetFishCount(gig.UniqueId);
        var fishCount = count > 0 ? $"({UIStrings.Caught_Counter} {count})" : "";

        DrawUtil.DrawCheckboxHeader($"{gig.Fish?.Name ?? UIStrings.None} {fishCount}", ref gig.Enabled, ImGuiTreeNodeFlags.FramePadding, () => {
            ImGui.Spacing();
            DrawFishSearchBar(gig);
            DrawDeleteButton(gig);
            DrawUtil.SpacingSeparator();
            gig.DrawOptions();
        });

        ImGui.Spacing();
    }

    private void DrawFishSearchBar(BaseGig gig) {
        using var _ = ImRaii.PushId("DrawFishSearchBar");
        var choices = gig.SpearfishingNotebookId == 0 || !GameRes.SpearfishingPoolsByNotebookId.TryGetValue(gig.SpearfishingNotebookId, out var pool)
            ? GameRes.SpearfishFishes
            : [.. pool.ItemIds.Select(id => GameRes.SpearfishFishesByItemId.GetValueOrDefault(id)).Where(fish => fish != null).Select(fish => fish!)];

        DrawUtil.DrawComboSelector(choices, fish => fish.Name, gig.Fish?.Name ?? UIStrings.None, fish => gig.Fish = fish);
    }

    private void DrawDeleteButton(BaseGig gig) {
        ImGui.SameLine();
        using (ImRaii.PushFont(UiBuilder.IconFont)) {
            if (ImGuiComponents.IconButton(FontAwesomeIcon.Trash) && ImGui.GetIO().KeyShift) {
                RemoveItem(gig.UniqueId);
                Configuration.Save();
            }
        }

        ImGui.TooltipOnHover(UIStrings.HoldShiftToDelete);
    }

    public static string GetPoolName(uint notebookId)
        => notebookId == 0 ? "Any Pool" : GameRes.SpearfishingPoolsByNotebookId.TryGetValue(notebookId, out var pool) ? pool.Name : $"Unknown Pool #{notebookId}";
}
