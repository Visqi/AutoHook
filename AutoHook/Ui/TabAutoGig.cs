using AutoHook.Spearfishing;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility.Raii;
using FFXIVClientStructs.FFXIV.Common.Math;

namespace AutoHook.Ui;

internal class TabAutoGig : BaseTab {
    public override string TabName => "Spearfishing Presets";
    public override bool Enabled => true;

    public override OpenWindow Type => OpenWindow.AutoGig;

    private readonly SpearFishingPresets _gigCfg = Service.Configuration.AutoGigConfig;

    public override void DrawHeader() {
        DrawTabDescription(UIStrings.TabAutoGigDescription);

        DrawUtil.DrawCheckboxTree(UIStrings.EnableAutoGig, ref _gigCfg.AutoGigEnabled, () => {
            if (_gigCfg is { AutoGigEnabled: true, AutoGigHideOverlay: true }) {
                _gigCfg.AutoGigHideOverlay = false;
                Service.Save();
            }

            DrawUtil.Checkbox(UIStrings.HideOverlayDuringSpearfishing, ref _gigCfg.AutoGigHideOverlay,
                UIStrings.AutoGigHideOverlayHelpMarker);

            DrawUtil.Checkbox(UIStrings.DrawFishHitbox, ref _gigCfg.AutoGigDrawFishHitbox);

            DrawUtil.Checkbox(UIStrings.DrawGigHitbox, ref _gigCfg.AutoGigDrawGigHitbox);

            DrawUtil.DrawTreeNodeEx("Automatic actions", () => {
                var actionX = ImGui.GetCursorPosX();
                ImGui.SetCursorPosX(actionX);
                _gigCfg.ThaliaksFavor.DrawConfig();
                ImGui.SetCursorPosX(actionX);
                _gigCfg.Cordial.DrawConfigWithLabel("Cordials");
                ImGui.SetCursorPosX(actionX);
                _gigCfg.NatureBountyBeforeFishAction.DrawConfigWithLabel(UIStrings.UseNaturesBounty);
            });

            DrawUtil.DrawCheckboxTree(UIStrings.CatchEverything, ref _gigCfg.CatchAll, () => {
                _gigCfg.CatchAllConditionSet = ConditionUi.DrawConditionSet(UIStrings.Conditions, _gigCfg.CatchAllConditionSet, ConditionScope.Spearfishing, showAdvanced: true);
                _gigCfg.CatchAllNaturesBountyAction.DrawConfigWithLabel(UIStrings.UseNaturesBounty);
            }, UIStrings.IgnoresPresets);
        });

        ImGui.Spacing();
        ImGui.TextWrapped(UIStrings.Current_Selected_Preset);
        DrawPresetSelector();
    }

    public override void Draw() {
        using var items = ImRaii.Child($"###ag_cfg1", Vector2.Zero, true);
        if (_gigCfg.SelectedPreset is { } selectedPreset) {
            if (_gigCfg.CatchAll) {
                ImGui.TextColored(ImGuiColors.DalamudYellow, UIStrings.CatchAllNotice);
            }

            var poolIds = new List<uint> { 0 };
            poolIds.AddRange(GameRes.SpearfishingPoolsByNotebookId.Keys.Where(id => id != 0).OrderBy(AutoGigConfig.GetPoolName));
            DrawUtil.DrawComboSelector(poolIds, AutoGigConfig.GetPoolName, AutoGigConfig.GetPoolName(selectedPreset.SelectedAddPoolId), id => selectedPreset.SelectedAddPoolId = id);
            ImGui.SameLine();

            var poolAlreadyAdded = selectedPreset.Gigs.Any(gig => gig.SpearfishingNotebookId == selectedPreset.SelectedAddPoolId);
            using (ImRaii.Disabled(poolAlreadyAdded)) {
                if (ImGui.SmallIconButton(FontAwesomeIcon.Plus)) {
                    selectedPreset.AddItem(new BaseGig(0) {
                        SpearfishingNotebookId = selectedPreset.SelectedAddPoolId,
                    });
                    Service.Save();
                }
            }
            DrawUtil.HoveredTooltip("Add pool");

            ImGui.SameLine();

            ImGui.SetNextItemWidth(90.Scaled());
            if (ImGui.InputInt(UIStrings.GigHitbox, ref selectedPreset.HitboxSize)) {
                selectedPreset.HitboxSize = Math.Max(0, Math.Min(selectedPreset.HitboxSize, 300));
                Service.Save();
            }

            DrawUtil.DrawTreeNodeEx("Preset actions", () => {
                var actionX = ImGui.GetCursorPosX();
                ImGui.SetCursorPosX(actionX);
                selectedPreset.Collect.DrawConfig();
                ImGui.SetCursorPosX(actionX);
                selectedPreset.ThaliaksFavor.DrawConfig();
                ImGui.SetCursorPosX(actionX);
                selectedPreset.Cordial.DrawConfigWithLabel("Cordials");
            });
            DrawUtil.Checkbox("Retain counters between pools", ref selectedPreset.RetainCountersBetweenSessions);
            if (ImGui.Button("Reset caught counters"))
                selectedPreset.ResetCounter();

            DrawUtil.SpacingSeparator();

            selectedPreset.DrawOptions();
        }
    }

    public void DrawPresetSelector() {
        DrawUtil.DrawComboSelectorPreset(_gigCfg);
        ImGui.SameLine();
        DrawUtil.DrawAddNewPresetButton(_gigCfg);
        ImGui.SameLine();
        DrawUtil.DrawImportExport(_gigCfg);
        ImGui.SameLine();
        DrawUtil.DrawDeletePresetButton(_gigCfg);
    }
}
