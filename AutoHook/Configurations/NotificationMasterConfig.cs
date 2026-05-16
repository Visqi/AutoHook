using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace AutoHook.Configurations;

public class NotificationMasterConfig {
    public bool Enabled;

    public bool DisplayToastNotification = true;
    public string ToastText = "";

    public bool FlashTaskbarIcon;

    public bool PlaySound;
    public string SoundPath = "";
    public float SoundVolume = 1f;
    public bool SoundRepeat;
    public bool StopSoundOnceFocused = true;

    public bool StopSound;
    public bool BringGameForeground;

    public void DrawConfig(string fallbackText) {
        var hasPlugin = Svc.PluginInterface.IsPluginLoaded("NotificationMaster");
        DrawUtil.DrawCheckboxTree("Notify On Success", ref Enabled,
            () => {
                using var disabled = ImRaii.Disabled(!hasPlugin);
                DrawUtil.Checkbox("Display toast notification", ref DisplayToastNotification);
                if (DisplayToastNotification) {
                    using var indent = ImRaii.PushIndent();

                    var text = string.IsNullOrWhiteSpace(ToastText) ? fallbackText : ToastText;

                    ImGui.SetNextItemWidth(320 * ImGuiHelpers.GlobalScale);
                    if (ImGui.InputText("Toast text", ref text, 260)) {
                        ToastText = text;
                        Service.Save();
                    }
                }

                DrawUtil.Checkbox("Flash taskbar icon", ref FlashTaskbarIcon);
                DrawUtil.Checkbox("Bring game foreground", ref BringGameForeground);

                DrawUtil.Checkbox("Play sound", ref PlaySound);
                if (PlaySound) {
                    using var indent = ImRaii.PushIndent();

                    ImGui.SetNextItemWidth(360 * ImGuiHelpers.GlobalScale);
                    if (ImGui.InputText("Sound path", ref SoundPath, 512))
                        Service.Save();

                    var volume = SoundVolume;
                    ImGui.SetNextItemWidth(120 * ImGuiHelpers.GlobalScale);
                    if (ImGui.SliderFloat("Volume", ref volume, 0f, 1f, "%.2f")) {
                        SoundVolume = volume;
                        Service.Save();
                    }

                    DrawUtil.Checkbox("Repeat sound", ref SoundRepeat);
                    DrawUtil.Checkbox("Stop sound once focused", ref StopSoundOnceFocused);
                }

                DrawUtil.Checkbox("Stop sound", ref StopSound);
            });
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled) && !hasPlugin) {
            using var tooltip = ImRaii.Tooltip();
            if (tooltip.Alive) {
                ImGui.TextUnformatted("NotificationMaster not installed. Settings below will have no effect");
            }
        }
    }
}
