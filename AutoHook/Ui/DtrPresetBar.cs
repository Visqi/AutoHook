using Dalamud.Game.Gui.Dtr;
using Dalamud.Game.Text;
using Dalamud.Plugin.Services;

namespace AutoHook.Ui;

public sealed class DtrPresetBar : IDisposable
{
    private string _text = string.Empty;
    private readonly IDtrBarEntry _dtrBarEntry;

    public DtrPresetBar(IDtrBar dtrBar)
    {
        _dtrBarEntry = dtrBar.Get("AutoHookPresets");
        _dtrBarEntry.OnClick += HandleClick;
    }

    private void HandleClick(DtrInteractionEvent @event)
    {
        if (@event.ClickType == MouseClickType.Left)
        {
            if (Service.Configuration.HookPresets.SelectedPreset == null) return;
            var totalPresets = Service.Configuration.HookPresets.CustomPresets.Count;
            var selectedPresetIndex = Service.Configuration.HookPresets.CustomPresets.IndexOf(Service.Configuration.HookPresets.SelectedPreset!);
            Service.Configuration.HookPresets.SelectedPreset = Service.Configuration.HookPresets.CustomPresets[(selectedPresetIndex + 1) % totalPresets];

            Service.Configuration.Save();
        }
        else if (@event.ClickType == MouseClickType.Right)
        {
            if (Service.Configuration.HookPresets.SelectedPreset == null) return;
            var totalPresets = Service.Configuration.HookPresets.CustomPresets.Count;
            var selectedPresetIndex = Service.Configuration.HookPresets.CustomPresets.IndexOf(Service.Configuration.HookPresets.SelectedPreset!);
            Service.Configuration.HookPresets.SelectedPreset = Service.Configuration.HookPresets.CustomPresets[(selectedPresetIndex - 1) % totalPresets];

            Service.Configuration.Save();
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
        if (!Service.Configuration.DtrPresetBarEnabled)
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

        string text = $"{SeIconChar.Collectible.ToIconString()} {Service.Configuration.HookPresets.SelectedPreset?.PresetName}";

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
