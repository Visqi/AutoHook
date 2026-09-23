using AutoHook.Conditions;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Windowing;
using ECommons.Automation;
using ECommons.Automation.NeoTaskManager;
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
    private Guid _lastGigEntryId;

    private readonly SpearFishingPresets _gigCfg = Service.Configuration.AutoGigConfig;

    public static string Gig = "Gig";

    private readonly TaskManager _taskManager = new() {
        DefaultConfiguration = { TimeLimitMS = 10000, ShowDebug = false }
    };

    public AutoGig() : base(@"SpearfishingHelper", WindowFlags, true) {
        _gigCfg.PrepareActions();
        Service.WindowSystem.AddWindow(this);
        Service.WorldState.Modified += OnWorldStateModified;
        IsOpen = true;
        Gig = LuminaAction.GetRow(IDs.Actions.Gig).Name.ToString();
    }

    public void Dispose() {
        Service.WorldState.Modified -= OnWorldStateModified;
        Service.WindowSystem.RemoveWindow(this);
        Configuration.FlushAsync().GetAwaiter().GetResult();
    }

    public override void Draw() {
        if (!_gigCfg.AutoGigHideOverlay || _gigCfg.AutoGigEnabled)
            DrawFishOverlay();
    }

    public void DrawSettings() {
        if (ImGui.Checkbox(UIStrings.Enable_AutoGig, ref _gigCfg.AutoGigEnabled))
            Service.Save();

        var selectedPreset = _gigCfg.SelectedPreset;
        ImGui.SameLine();
        DrawUtil.Checkbox(UIStrings.CatchEverything, ref _gigCfg.CatchAll, UIStrings.IgnoresPresets);
        PluginUi.ShowKofi();
        DrawUtil.DrawComboSelector(_gigCfg.Presets, preset => preset.PresetName, _gigCfg.SelectedPreset?.PresetName ?? UIStrings.None, gig => _gigCfg.SelectedPreset = gig);

        ImGui.SetNextItemWidth(90.Scaled());
        if (selectedPreset != null) {
            ImGui.SameLine();
            ImGui.SetNextItemWidth(90.Scaled());
            if (ImGui.InputInt(UIStrings.Hitbox + @" ", ref selectedPreset.HitboxSize)) {
                selectedPreset.HitboxSize = Math.Max(0, Math.Min(selectedPreset.HitboxSize, 300));
                Service.Save();
            }
        }

        ImGui.SameLine();

        if (_gigCfg.CatchAll)
            ImGui.TextColored(ImGuiColors.DalamudYellow, UIStrings.CatchAllGigWindow);
    }

    private unsafe void DrawFishOverlay() {
        if (!Svc.GameGui.TryGetAddon<AddonSpearFishing>("SpearFishing", out var addon)) return;
        var isOpen = addon != null && addon->AtkUnitBase.WindowNode != null;

        if (!isOpen)
            return;

        ImGui.SetNextWindowPos(new Vector2(addon->AtkUnitBase.X + 5, addon->AtkUnitBase.Y - 65));
        if (ImGui.Begin("gig###gig", ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoTitleBar)) {
            DrawSettings();
            ImGui.End();
        }

        if (Service.Configuration.PluginEnabled && _gigCfg is { AutoGigEnabled: true, }) {
            var selectedPreset = _gigCfg.SelectedPreset;

            TrySessionActions(selectedPreset);
            GigFish(addon, addon->Fish[0], addon->GetNodeById(Fish1NodeId));
            GigFish(addon, addon->Fish[1], addon->GetNodeById(Fish2NodeId));
            GigFish(addon, addon->Fish[2], addon->GetNodeById(Fish3NodeId));
        }
    }

    private unsafe void GigFish(AddonSpearFishing* addon, AddonSpearFishing.FishInfo info, AtkResNode* node) {
        if (node == null)
            return;

        var drawList = ImGui.GetWindowDrawList();
        var gigHitbox = _gigCfg.SelectedPreset?.HitboxSize ?? 0;
        var fishLines = addon->GetNodeById(FishLaneNodeId);
        if (fishLines == null)
            return;

        DrawGigHitbox(fishLines, drawList, gigHitbox);

        if (!info.Available)
            return;

        var fish = _gigCfg.CatchAll ? GetCatchAllGig() : CheckFish(info);

        if (fish == null || !fish.Enabled || !fish.GigConditionSet.PassesOrUnconfigured())
            return;

        var naturesBounty = _gigCfg.CatchAll ? _gigCfg.CatchAllNaturesBountyAction : fish.NaturesBounty;
        if (naturesBounty.IsAvailableToCast())
            PlayerRes.CastActionDelayed(naturesBounty.Id, naturesBounty.ActionType, naturesBounty.GetName());

        var laneOriginX = fishLines->X * _uiScale;
        var centerX = laneOriginX + fishLines->Width * fishLines->ScaleX * _uiScale / 2f;
        var anchor = info.InverseDirection ? 0.5f + fish.RightOffset / 10 : 0.4f - fish.LeftOffset / 10;
        var fishHitbox = laneOriginX + node->X * _uiScale + node->Width * node->ScaleX * _uiScale * anchor;

        DrawFishHitbox(fishLines, drawList, fishHitbox);

        if (fishHitbox >= centerX - gigHitbox && fishHitbox <= centerX + gigHitbox) {
            _lastGigEntryId = _gigCfg.CatchAll ? Guid.Empty : fish.UniqueId;
            _taskManager.Enqueue(() => { Chat.ExecuteCommand($"/ac \"{Gig}\""); });
        }
    }

    private BaseGig? CheckFish(AddonSpearFishing.FishInfo info) {
        var notebookId = Service.WorldState.Spearfishing.Spot.NotebookId;
        return _gigCfg.SelectedPreset?.FindGigForPool(notebookId, (Enums.SpearfishSpeed)info.Speed, (Enums.SpearfishSize)info.Size);
    }

    private BaseGig? GetCatchAllGig() => _gigCfg.CatchAllConditionSet.PassesOrUnconfigured() ? new BaseGig(0) { Enabled = true } : null;

    private void TrySessionActions(AutoGigConfig? selectedPreset) {
        if (selectedPreset?.Collect.IsAvailableToCast() == true)
            PlayerRes.CastActionDelayed(selectedPreset.Collect.Id, selectedPreset.Collect.ActionType, selectedPreset.Collect.GetName());
        if (_gigCfg.NatureBountyBeforeFishAction.IsAvailableToCast())
            PlayerRes.CastActionDelayed(_gigCfg.NatureBountyBeforeFishAction.Id, _gigCfg.NatureBountyBeforeFishAction.ActionType, _gigCfg.NatureBountyBeforeFishAction.GetName());
        var thaliaksFavor = selectedPreset is { ThaliaksFavor.Enabled: true } ? selectedPreset.ThaliaksFavor : _gigCfg.ThaliaksFavor;
        if (thaliaksFavor.IsAvailableToCast())
            PlayerRes.CastActionDelayed(thaliaksFavor.Id, thaliaksFavor.ActionType, thaliaksFavor.GetName());

        var cordial = selectedPreset is { Cordial.Enabled: true } ? selectedPreset.Cordial : _gigCfg.Cordial;
        if (cordial.IsAvailableToCast())
            PlayerRes.CastActionDelayed(cordial.Id, cordial.ActionType, cordial.GetName());
    }

    private void OnWorldStateModified(WorldState.Operation op) {
        if (op is SpearfishingInfo.OpAddFishCaught caught) {
            var preset = _gigCfg.SelectedPreset;
            if (preset == null)
                return;

            var matched = _lastGigEntryId == Guid.Empty ? null : preset.Gigs.FirstOrDefault(gig => gig.UniqueId == _lastGigEntryId && gig.Fish?.ItemId == caught.FishId);
            matched ??= preset.GetGigsForPool(Service.WorldState.Spearfishing.Spot.NotebookId).FirstOrDefault(gig => gig.Fish?.ItemId == caught.FishId);
            if (matched != null)
                SpearfishingCounterHelper.AddFishCount(matched.UniqueId, caught.Amount);
            _lastGigEntryId = Guid.Empty;
        }
        else if (op is SpearfishingInfo.OpEndSession) {
            _lastGigEntryId = Guid.Empty;
            if (_gigCfg.SelectedPreset is not { RetainCountersBetweenSessions: true }) {
                SpearfishingCounterHelper.ResetAll();
                Service.WorldState.Execute(new SpearfishingInfo.OpResetFishCaught());
            }
        }
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
