using AutoHook.Modules.Gig;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using System.Numerics;
using LuminaAction = Lumina.Excel.Sheets.Action;

namespace AutoHook.Spearfishing;

internal class AutoGig : Window, IDisposable {
    private const uint FishLaneNodeId = 43;
    private const uint Fish1NodeId = 61;
    private const uint Fish2NodeId = 60;
    private const uint Fish3NodeId = 59;

    private const ImGuiWindowFlags WindowFlags = ImGuiWindowFlags.NoDecoration
                                                 | ImGuiWindowFlags.NoInputs
                                                 | ImGuiWindowFlags.AlwaysAutoResize
                                                 | ImGuiWindowFlags.NoFocusOnAppearing
                                                 | ImGuiWindowFlags.NoNavFocus
                                                 | ImGuiWindowFlags.NoBackground;

    private float _uiScale = 1;
    private Vector2 _uiPos = Vector2.Zero;
    private Vector2 _uiSize = Vector2.Zero;

    private int currentNode = 0;

    private readonly SpearFishingPresets _gigCfg = Service.Configuration.AutoGigConfig;

    public static string Gig = "Gig";

    private GigFishingModule GigFishing => Service.FishingSessions.Gig;

    public AutoGig() : base(@"SpearfishingHelper", WindowFlags, true) {
        _gigCfg.PrepareActions();
        Service.WindowSystem.AddWindow(this);
        IsOpen = true;
        Gig = LuminaAction.GetRow(IDs.Actions.Gig).Name.ToString();
    }

    public void Dispose() {
        Service.WindowSystem.RemoveWindow(this);
        Configuration.FlushAsync().GetAwaiter().GetResult();
    }

    public override void Draw() {
        DrawFishOverlay();
    }

    public void DrawSettings() {
        var selectedPreset = _gigCfg.SelectedPreset;
        ImGui.TextColored(ImGuiColors.DalamudOrange,
            selectedPreset?.PresetName ?? UIStrings.CatchEverything);
        PluginUi.ShowKofi();

        ImGui.SetNextItemWidth(220.Scaled());
        var preview = selectedPreset?.PresetName ?? UIStrings.CatchEverything;
        using (var combo = ImRaii.Combo("###gigOverlayPreset", preview)) {
            if (combo) {
                if (ImGui.Selectable(UIStrings.CatchEverything, selectedPreset == null))
                    _gigCfg.SelectedPreset = null;
                foreach (var preset in _gigCfg.Presets) {
                    if (ImGui.Selectable(preset.PresetName, selectedPreset?.UniqueId == preset.UniqueId))
                        _gigCfg.SelectedPreset = preset;
                }
            }
        }

        ImGui.SameLine();
        ImGui.SetNextItemWidth(90.Scaled());
        var hitbox = _gigCfg.ActiveHitboxSize;
        if (ImGui.InputInt(UIStrings.Hitbox + @" ", ref hitbox)) {
            hitbox = Math.Max(0, Math.Min(hitbox, 300));
            if (selectedPreset != null)
                selectedPreset.HitboxSize = hitbox;
            else
                _gigCfg.GlobalHitboxSize = hitbox;
            Service.Save();
        }

        if (_gigCfg.IsCatchAllActive)
            ImGui.TextColored(ImGuiColors.DalamudYellow, UIStrings.CatchAllGigWindow);
    }

    private unsafe void DrawFishOverlay() {
        if (!Svc.GameGui.TryGetAddon<AddonSpearFishing>("SpearFishing", out var addon)) return;
        var isOpen = addon != null && addon->AtkUnitBase.WindowNode != null;

        if (!isOpen)
            return;

        if (!_gigCfg.AutoGigHideOverlay) {
            ImGui.SetNextWindowPos(new Vector2(addon->AtkUnitBase.X + 5, addon->AtkUnitBase.Y - 65));
            if (ImGui.Begin("gig###gig", ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoTitleBar)) {
                DrawSettings();
                ImGui.End();
            }
        }

        if (!Service.Configuration.PluginEnabled)
            return;

        DrawFishHitboxes(addon, addon->Fish[0], addon->GetNodeById(Fish1NodeId));
        DrawFishHitboxes(addon, addon->Fish[1], addon->GetNodeById(Fish2NodeId));
        DrawFishHitboxes(addon, addon->Fish[2], addon->GetNodeById(Fish3NodeId));
    }

    private unsafe void DrawFishHitboxes(AddonSpearFishing* addon, AddonSpearFishing.FishInfo info, AtkResNode* node) {
        if (node == null)
            return;

        var drawList = ImGui.GetForegroundDrawList();
        var gigHitbox = _gigCfg.ActiveHitboxSize;
        var fishLines = addon->GetNodeById(FishLaneNodeId);
        if (fishLines == null)
            return;

        DrawGigHitbox(fishLines, drawList, gigHitbox);

        if (!info.Available)
            return;

        var fish = GigFishing.Gig.FindGigForFish(info);
        if (fish == null || !GigFishing.Gig.ShouldGig(fish))
            return;

        var laneOriginX = fishLines->X * _uiScale;
        var anchor = info.InverseDirection ? 0.5f + fish.RightOffset / 10 : 0.4f - fish.LeftOffset / 10;
        var fishHitbox = laneOriginX + node->X * _uiScale + node->Width * node->ScaleX * _uiScale * anchor;

        DrawFishHitbox(fishLines, drawList, fishHitbox);
    }

    private unsafe void DrawGigHitbox(AtkResNode* fishLines, ImDrawListPtr drawList, int gigHitbox) {
        if (!_gigCfg.AutoGigDrawGigHitbox)
            return;

        var laneOriginX = fishLines->X * _uiScale;
        var startX = laneOriginX + fishLines->Width * fishLines->ScaleX * _uiScale / 2f;
        var centerY = fishLines->Y * _uiScale;
        var endY = fishLines->Height * _uiScale;

        var lineStart = _uiPos + new Vector2(startX - gigHitbox, centerY);
        var lineEnd = lineStart + new Vector2(0, endY);
        drawList.AddLine(lineStart, lineEnd, 0xFF0000C0, 1.Scaled());

        lineStart = _uiPos + new Vector2(startX + gigHitbox, centerY);
        lineEnd = lineStart + new Vector2(0, endY);
        drawList.AddLine(lineStart, lineEnd, 0xFF0000C0, 1.Scaled());
    }

    private unsafe void DrawFishHitbox(AtkResNode* fishLines, ImDrawListPtr drawList, float fishHitbox) {
        if (!_gigCfg.AutoGigDrawFishHitbox)
            return;

        var lineStart = _uiPos + new Vector2(fishHitbox, fishLines->Y * _uiScale);
        var lineEnd = lineStart + new Vector2(0, fishLines->Height * _uiScale);
        drawList.AddLine(lineStart, lineEnd, 0xFF20B020, 1.Scaled());
    }

    private bool _isOpen = false;

    public override unsafe bool DrawConditions() {
        var lastOpen = _isOpen;

        if (!Svc.GameGui.TryGetAddon<AtkUnitBase>("SpearFishing", out var addon)) {
            _isOpen = false;
            return false;
        }

        _isOpen = addon->WindowNode != null;

        if (!_isOpen)
            return false;

        if (_isOpen != lastOpen)
            SetFishTargets();

        return true;
    }

    private void SetFishTargets() {
        currentNode = 0;
        if (Svc.Targets.Target is { ObjectKind: ObjectKind.GatheringPoint, BaseId: var id })
            currentNode = (int)id;
    }

    public override unsafe void PreDraw() {
        if (!Svc.GameGui.TryGetAddon<AtkUnitBase>("SpearFishing", out var addon)) return;
        _uiScale = addon->Scale;
        _uiPos = new Vector2(addon->X, addon->Y);
        _uiSize = new Vector2(addon->WindowNode->AtkResNode.Width * _uiScale, addon->WindowNode->AtkResNode.Height * _uiScale);

        Position = _uiPos;
        SizeConstraints = new WindowSizeConstraints {
            MinimumSize = _uiSize,
            MaximumSize = Vector2.One * 10000,
        };
    }
}
