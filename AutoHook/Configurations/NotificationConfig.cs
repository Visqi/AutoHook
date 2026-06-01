using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace AutoHook.Configurations;

public record class NotificationConfig {
    public bool Enabled;
    public bool DisplayToastNotification;
    public string ToastText = "";
    public bool FlashTaskbarIcon;
    public bool BringGameForeground;
    public bool BeepOnSuccess;

    public void DrawConfig(string fallbackText) {
        var hasPlugin = Svc.Interface.IsPluginLoaded("NotificationMaster");
        DrawUtil.DrawCheckboxTree("Notify On Success", ref Enabled,
            () => {
                DrawUtil.Checkbox("Play a beep", ref BeepOnSuccess);

                using var disabled = ImRaii.Disabled(!hasPlugin);
                DrawUtil.Checkbox("Display toast notification", ref DisplayToastNotification);
                if (DisplayToastNotification) {
                    using var indent = ImRaii.PushIndent();

                    var text = string.IsNullOrWhiteSpace(ToastText) ? fallbackText : ToastText;

                    ImGui.SetNextItemWidth(320.Scaled());
                    if (ImGui.InputText("Toast text", ref text, 260)) {
                        ToastText = text;
                        Service.Save();
                    }
                }

                DrawUtil.Checkbox("Flash taskbar icon", ref FlashTaskbarIcon);
                DrawUtil.Checkbox("Bring game to foreground", ref BringGameForeground);
            });
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled) && !hasPlugin) {
            using var tooltip = ImRaii.Tooltip();
            if (tooltip.Alive) {
                ImGui.TextUnformatted("NotificationMaster not installed. NotificationMaster-specific options below will have no effect");
            }
        }
    }
}
