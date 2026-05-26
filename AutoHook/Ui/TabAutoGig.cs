using AutoHook.Spearfishing;
using Dalamud.Bindings.ImGui;
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

            //_gigCfg.Cordial.DrawConfig();
            _gigCfg.ThaliaksFavor.DrawConfig();

            DrawUtil.Checkbox(UIStrings.CatchEverything, ref _gigCfg.CatchAll, UIStrings.IgnoresPresets);

            if (_gigCfg.CatchAll) {
                ImGui.Text($" └");
                ImGui.SameLine();
                DrawUtil.Checkbox(UIStrings.Use_Natures_Bounty, ref _gigCfg.CatchAllNaturesBounty,
                    UIStrings.CatchAllNaturesBountyHelpText);
            }

            DrawUtil.Checkbox(UIStrings.NBBeforeFish, ref _gigCfg.NatureBountyBeforeFish, UIStrings.NBBeforeFishHelpText);

            ImGui.TextColored(ImGuiColors.DalamudYellow, UIStrings.AutoCordialPandoras);
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

            // add new gig button
            if (ImGui.Button(UIStrings.Add_new_fish)) {
                selectedPreset.AddItem(new BaseGig(0));
                Service.Save();
            }

            ImGui.SameLine();

            ImGui.SetNextItemWidth(90);
            if (ImGui.InputInt(UIStrings.GigHitbox, ref selectedPreset.HitboxSize)) {
                selectedPreset.HitboxSize = Math.Max(0, Math.Min(selectedPreset.HitboxSize, 300));
                Service.Save();
            }

            DrawUtil.Checkbox(UIStrings.Collect, ref selectedPreset.KeepCollectorsGloveOn, UIStrings.CollectHelpText);

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
