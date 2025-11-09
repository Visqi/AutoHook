using Dalamud.Interface.Utility.Raii;

namespace AutoHook.Ui;

internal class TabGlobalPreset : BaseTab
{
    public override bool Enabled => true;
    public override string TabName => UIStrings.GlobalPreset;
    public override OpenWindow Type => OpenWindow.Global;

    public override void DrawHeader()
    {
        DrawTabDescription(UIStrings.TabGlobalPreset_Description);
    }

    public override void Draw()
    {
        using var id = ImRaii.PushId("TabBarsGlobal");
        var preset = Service.Configuration.HookPresets.DefaultPreset;
        preset.DrawOptions();
    }
}