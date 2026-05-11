using ECommons.EzIpcManager;

namespace AutoHook.IPC;

public class NotificationMasterIPC {
    [EzIPC] private readonly Func<string, string, string, bool>? DisplayToastNotification;
    [EzIPC] private readonly Func<string, bool>? FlashTaskbarIcon;
    [EzIPC] private readonly Func<string, string, float, bool, bool, bool>? PlaySound;
    [EzIPC] private readonly Func<string, bool>? StopSound;
    [EzIPC] private readonly Func<string, bool>? BringGameForeground;

    public NotificationMasterIPC() {
        EzIPC.Init(this);
    }

    public void Notify(NotificationMasterConfig cfg, string text) {
        if (!cfg.Enabled)
            return;

        if (cfg.DisplayToastNotification)
            DisplayToastNotification?.Invoke(Svc.PluginInterface.Manifest.Name, Svc.PluginInterface.Manifest.Name, string.IsNullOrWhiteSpace(cfg.ToastText) ? text : cfg.ToastText);

        if (cfg.FlashTaskbarIcon)
            FlashTaskbarIcon?.Invoke(Svc.PluginInterface.Manifest.Name);

        if (cfg.PlaySound && !string.IsNullOrWhiteSpace(cfg.SoundPath))
            PlaySound?.Invoke(Svc.PluginInterface.Manifest.Name, cfg.SoundPath, Math.Clamp(cfg.SoundVolume, 0f, 1f), cfg.SoundRepeat, cfg.StopSoundOnceFocused);

        if (cfg.StopSound)
            StopSound?.Invoke(Svc.PluginInterface.Manifest.Name);

        if (cfg.BringGameForeground)
            BringGameForeground?.Invoke(Svc.PluginInterface.Manifest.Name);
    }
}
