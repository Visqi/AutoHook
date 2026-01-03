using Dalamud.Game.Gui.Dtr;
using Dalamud.Game.Text;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using System;
using System.Collections.Generic;
using System.Text;

namespace AutoHook.Ui;

public sealed class DtrBar : IDisposable
{
    private string _text = string.Empty;
    private readonly PluginUi _pluginUi;
    private readonly IDtrBarEntry _dtrBarEntry;

    public DtrBar(IDtrBar dtrBar, PluginUi pluginUi)
    {
        _pluginUi = pluginUi;
        _dtrBarEntry = dtrBar.Get("AutoHook");
        _dtrBarEntry.OnClick += HandleClick;
    }

    private void HandleClick(DtrInteractionEvent @event)
    {
        if (@event.ModifierKeys == ClickModifierKeys.None)
        {
            if (@event.ClickType == MouseClickType.Left)
            {
                Service.Configuration.PluginEnabled = !Service.Configuration.PluginEnabled;
                Service.Configuration.Save();
            }
            else if (@event.ClickType == MouseClickType.Right)
            {
                _pluginUi.Toggle();
            }
        }
        else
        {
            if (@event.ClickType == MouseClickType.Left)
            {
                if (Service.Configuration.HookPresets.SelectedPreset == null) return;
                var totalPresets = Service.Configuration.HookPresets.CustomPresets.Count;
                var selectedPresetIndex = Service.Configuration.HookPresets.CustomPresets.IndexOf(Service.Configuration.HookPresets.SelectedPreset!);
                Service.Configuration.HookPresets.SelectedPreset = Service.Configuration.HookPresets.CustomPresets[(selectedPresetIndex - 1) % totalPresets];

                Service.Configuration.Save();
            }
            else if (@event.ClickType == MouseClickType.Right)
            {
                if (Service.Configuration.HookPresets.SelectedPreset == null) return;
                var totalPresets = Service.Configuration.HookPresets.CustomPresets.Count;
                var selectedPresetIndex = Service.Configuration.HookPresets.CustomPresets.IndexOf(Service.Configuration.HookPresets.SelectedPreset!);
                Service.Configuration.HookPresets.SelectedPreset = Service.Configuration.HookPresets.CustomPresets[(selectedPresetIndex + 1) % totalPresets];

                Service.Configuration.Save();
            }
        }
    }

    public void Dispose()
    {
        if (_dtrBarEntry != null)
        {
            _dtrBarEntry.OnClick -= HandleClick;
            Clear();
        }
    }

    public void Update(IFramework _)
    {
        if (!Service.Configuration.DtrBarEnabled)
        {
            if (_dtrBarEntry.Shown)
            {
                Clear();
            }
            return;
        }

        if (!_dtrBarEntry.Shown)
        {
            _dtrBarEntry.Shown = true;
        }

        string text;
        string pluginStatus = Service.Configuration.PluginEnabled ? UIStrings.Enabled : UIStrings.Disabled;
        if (Service.Configuration.DtrShowCurrentPreset)
        {
            text = $"{SeIconChar.BoxedStar.ToIconString()} {pluginStatus} - {Service.Configuration.HookPresets.SelectedPreset?.PresetName}";
        }
        else
        {
            text = $"{SeIconChar.BoxedStar.ToIconString()} {pluginStatus}";
        }

        if (!string.Equals(text, _text, StringComparison.Ordinal))
        {
            _text = text;
            _dtrBarEntry.Text = text;
        }
    }

    private void Clear()
    {
        _dtrBarEntry.Shown = false;
        _dtrBarEntry.Text = null;
    }
}
