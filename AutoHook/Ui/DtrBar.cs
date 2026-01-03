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

        var pluginStatus = Service.Configuration.PluginEnabled ? UIStrings.Enabled : UIStrings.Disabled;
        var text = $"{SeIconChar.BoxedStar.ToIconString()} {pluginStatus}";

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
