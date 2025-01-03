using System;
using System.Collections.Generic;
using System.Linq;
using AutoHook.Classes;
using AutoHook.Configurations;
using AutoHook.Enums;
using AutoHook.Fishing;
using AutoHook.Resources.Localization;
using AutoHook.Utils;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility.Raii;
using ECommons.ImGuiMethods;
using ImGuiNET;
using Newtonsoft.Json;

namespace AutoHook.Ui;

public class TabFishingPresets : BaseTab
{
    public override bool Enabled => true;
    public override string TabName => UIStrings.FishingPresets;

    public override OpenWindow Type => OpenWindow.FishingPreset;


    private static FishingPresets _basePreset = Service.Configuration.HookPresets;

    public static bool OpenPresetGen;
    private PresetCreator PresetCreator = new();

    public override void DrawHeader()
    {
        DrawTabDescription(UIStrings.TabPresets_DrawHeader_NewTabDescription);

        if (OpenPresetGen)
            DrawPresetGenTab();
    }

    private void DrawPresetGenTab()
    {
        ImGui.PushID(@"PresetGen");
        ImGui.SetNextItemWidth(500);
        if (ImGui.Begin(UIStrings.PresetGen, ref OpenPresetGen, ImGuiWindowFlags.AlwaysUseWindowPadding))
            PresetCreator.DrawPresetGenerator();

        ImGui.End();
        ImGui.PopID();
    }

    public override void Draw()
    {
        try
        {
            DrawList();
            /*if (Service.Configuration.ShowPresetsAsSidebar)
                DrawList();
            else
                DrawPresetSelectionDropdown();*/
        }
        catch (Exception e)
        {
            Service.PluginLog.Error(e.Message);
        }
    }

    private void DrawPresetSelectionDropdown()
    {
        //todo: add dropdown for preset selection
    }

    public static List<(string, List<CustomPresetConfig>)> OrganizeItems(List<CustomPresetConfig> items)
    {
        var myList = new List<(string, List<CustomPresetConfig>)>();

        foreach (var item in items)
        {
            if (string.IsNullOrEmpty(item.GroupName))
            {
                // Ungrouped item: Add as a file with an empty group name
                myList.Add(("", [item]));
            }
            else
            {
                // Grouped item: Find or create the folder
                var folder = myList.FirstOrDefault(x => x.Item1 == item.GroupName);

                if (folder == default)
                {
                    // Folder doesn't exist: Create a new one
                    folder = (item.GroupName, []);
                    myList.Add(folder);
                }

                // Add the item to the folder
                folder.Item2.Add(item);
            }
        }

        return myList;
    }


    private static BasePresetConfig? displayed = _basePreset.SelectedPreset ?? _basePreset.DefaultPreset;

    private void DrawList()
    {
        using (var table = ImRaii.Table($"###PresetTable", 2, ImGuiTableFlags.Resizable))
        {
            if (!table)
                return;

            ImGui.TableSetupColumn($"###OptionColumn", ImGuiTableColumnFlags.WidthStretch, 2f);
            ImGui.TableNextColumn();
            using (var left = ImRaii.Child($"###OptionSide"))
                DrawPresetOptions(displayed);

            ImGui.TableSetupColumn($"###PresetColumn", ImGuiTableColumnFlags.WidthStretch, 1f);
            ImGui.TableNextColumn();
            using (var right = ImRaii.Child($"###PresetSide"))
            {
                DrawPresetButtons();

                //DrawUtil.TextV(_basePreset.SelectedPreset?.PresetName ?? "No Preset Selected");
                using var list = ImRaii.ListBox("preset_list", ImGui.GetContentRegionAvail());
                if (!list)
                    return;

                DrawUtil.Info(UIStrings.GlobalPresetHelpText);
                ImGui.SameLine(0, 4);
                if (ImGui.Selectable(UIStrings.GlobalPreset,
                        displayed?.PresetName == _basePreset.DefaultPreset.PresetName,
                        ImGuiSelectableFlags.AllowDoubleClick))
                {
                    displayed = _basePreset.DefaultPreset;
                }

                ImGui.Separator();
                
                /*
                Dictionary<string, List<BasePresetConfig>> toDraw = new();
                var organized = OrganizeItems(_basePreset.CustomPresets);
                bool show = true;

                foreach (var entry in organized)
                {
                    bool empty = string.IsNullOrEmpty(entry.Item1);
                    for (var i = 0; i < entry.Item2.Count; i++)
                    {
                        using var id = ImRaii.PushId(i);
                        var preset = entry.Item2[i];

                        if (!empty)
                        {
                            if (ImGui.TreeNodeEx($"Name aa {preset.PresetName}###{preset.PresetName}", ImGuiTreeNodeFlags.FramePadding))
                                show = true;
                            else
                                show = false;
                        }

                        if (show)
                        {
                            //todo wth the f is this
                        }
                    }
                }*/

                for (var i = 0; i < _basePreset.PresetList.Count; i++)
                {
                    var preset = _basePreset.PresetList[i];
                    using var id = ImRaii.PushId(preset.UniqueId.ToString());
                    var selected = _basePreset.SelectedGuid == preset.UniqueId.ToString();
                    var color = selected ? ImGuiColors.DalamudOrange : ImGuiColors.DalamudWhite;
                    using (var a = ImRaii.PushColor(ImGuiCol.Text, color))
                    {
                        if (ImGui.Selectable((selected ? "> " : "") + preset.PresetName,
                                displayed?.UniqueId == preset.UniqueId,
                                ImGuiSelectableFlags.AllowDoubleClick))
                        {
                            displayed = preset;

                            if (ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
                            {
                                _basePreset.SelectedPreset = selected ? null : (CustomPresetConfig)preset;
                                Service.Save();
                            }
                        }
                    }

                    if (ImGui.BeginDragDropSource())
                    {
                        ImGuiDragDrop.SetDragDropPayload("PRESET_ORDER", i);
                        ImGui.Text($"Moving: {preset.PresetName}");
                        ImGui.EndDragDropSource();
                    }

                    if (ImGui.BeginDragDropTarget())
                    {
                        if (ImGuiDragDrop.AcceptDragDropPayload("PRESET_ORDER", out int itemIndex))
                        {
                            if (ImGui.IsMouseReleased(ImGuiMouseButton.Left))
                            {
                                _basePreset.SwapIndex(itemIndex, i);
                            }
                        }

                        ImGui.EndDragDropTarget();
                    }

                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip(UIStrings.RightClickOptions);

                    DrawPresetContext(preset);
                }
            }
        }
    }

    private void DrawItem(CustomPresetConfig preset, int i)
    {
        using var id = ImRaii.PushId(preset.UniqueId.ToString());
        var selected = _basePreset.SelectedGuid == preset.UniqueId.ToString();
        var color = selected ? ImGuiColors.DalamudOrange : ImGuiColors.DalamudWhite;
        using (var a = ImRaii.PushColor(ImGuiCol.Text, color))
        {
            if (ImGui.Selectable((selected ? "> " : "") + preset.PresetName,
                    displayed?.UniqueId == preset.UniqueId,
                    ImGuiSelectableFlags.AllowDoubleClick))
            {
                displayed = preset;

                if (ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
                {
                    _basePreset.SelectedPreset = selected ? null : (CustomPresetConfig)preset;
                    Service.Save();
                }
            }
        }

        if (ImGui.BeginDragDropSource())
        {
            ImGuiDragDrop.SetDragDropPayload("PRESET_ORDER", i); // Use IntPtr for payload
            ImGui.Text(preset.PresetName);
            ImGui.EndDragDropSource();
        }

        if (ImGui.BeginDragDropTarget())
        {
            if (ImGuiDragDrop.AcceptDragDropPayload("PRESET_ORDER", out int itemIndex))
            {
                if (ImGui.IsMouseReleased(ImGuiMouseButton.Left))
                {
                    _basePreset.SwapIndex(itemIndex, i);
                }
            }

            ImGui.EndDragDropTarget();
        }

        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(UIStrings.RightClickOptions);

        DrawPresetContext(preset);
    }

    private void DrawPresetOptions(BasePresetConfig? preset)
    {
        if (preset == null)
            return;

        using var id = ImRaii.PushId("TabBarsPreset");

        preset.DrawOptions();
    }

    private void DrawPresetButtons()
    {
        if (ImGuiComponents.IconButton(FontAwesomeIcon.ArrowsSpin))
            OpenPresetGen = !OpenPresetGen;

        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(UIStrings.PresetGenerator);

        ImGui.SameLine(0, 3);
        DrawUtil.DrawAddNewPresetButton(_basePreset);
        ImGui.SameLine(0, 3);
        DrawUtil.DrawImportPreset(_basePreset);
    }

    public static void DrawPresetContext(BasePresetConfig preset)
    {
        if (preset == null)
            return;

        if (!ImGui.BeginPopupContextItem(@$"PresetOptions###{preset.PresetName}"))
            return;

        var alreadySelected = _basePreset.SelectedPreset?.PresetName == preset.PresetName;
        if (ImGui.Selectable(!alreadySelected ? UIStrings.SetActive : UIStrings.Deselect))
        {
            _basePreset.SelectedPreset = alreadySelected ? null : (CustomPresetConfig)preset;
            Service.Save();
        }

        if (ImGui.Selectable(UIStrings.Rename, false, ImGuiSelectableFlags.DontClosePopups))
        {
            ImGui.OpenPopup(@$"PresetRenameName");
        }

        if (ImGui.Selectable(UIStrings.MakeACopy, false))
        {
            CopyPreset(preset);
        }

        DrawUtil.DrawRenamePreset(preset);

        if (ImGui.Selectable(UIStrings.ExportPresetToClipboard, false))
        {
            ImGui.SetClipboardText(Configuration.ExportPreset(preset));
            Notify.Success(UIStrings.PresetExportedToTheClipboard);
        }

        using (var disabled = ImRaii.Disabled(!ImGui.GetIO().KeyShift))
        {
            if (ImGui.Selectable(UIStrings.Delete, false, ImGuiSelectableFlags.DontClosePopups))
            {
                _basePreset.RemovePreset(preset.UniqueId);
                displayed = null;
                Service.Save();
            }
        }

        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip(UIStrings.HoldShiftToDelete);

        ImGui.EndPopup();
    }

    private static void CopyPreset(BasePresetConfig preset)
    {
        var json = JsonConvert.SerializeObject(preset);
        var copy = JsonConvert.DeserializeObject<CustomPresetConfig>(json);
        copy!.UniqueId = Guid.NewGuid();
        copy.PresetName = @$"Copy_{preset.PresetName}";
        _basePreset.AddNewPreset(copy);
        Service.Save();
    }
}