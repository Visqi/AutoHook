using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using ECommons.Automation.NeoTaskManager;

namespace AutoHook;

public class Service {
    public static void Initialize(IDalamudPluginInterface pluginInterface)
        => pluginInterface.Create<Service>();

    public const string PluginName = "AutoHook";
    public const string GlobalPresetName = "Global Preset";
    public static string _status = @"";

    public static WorldState WorldState { get; set; } = null!;
    /// <summary>Pushed each frame before fishing logic reads <see cref="WorldState"/> (framework tick).</summary>
    public static WorldStateUpdater WorldStateUpdater { get; set; } = null!;
    public static Configuration Configuration { get; set; } = null!;
    public static WindowSystem WindowSystem { get; } = new(PluginName);
    public static BaitFishClass LastCatch { get; set; } = new(@"-", -1);
    public static AutoCollectables AutoCollectables { get; set; } = null!;
    public static NotificationMasterAPI.NotificationMasterApi NotificationMaster { get; set; } = null!;

    public static string Status {
        get => _status;
        set => _status = value;
    }

    public static readonly TaskManager TaskManager = new() {
        DefaultConfiguration = { TimeLimitMS = 5000 }
    };

    public static void Save() {
        Configuration.Save();
    }

    private const int MaxLogSize = 50;
    public static Queue<string> LogMessages = new();
    public static bool OpenConsole;
    public static void PrintDebug(string msg) {
        if (LogMessages.Count >= MaxLogSize) {
            LogMessages.Dequeue();
        }

        LogMessages.Enqueue(msg);
        Svc.Log.Debug(msg);
    }

    public static void PrintVerbose(string msg) {
        if (LogMessages.Count >= MaxLogSize) {
            LogMessages.Dequeue();
        }

        LogMessages.Enqueue(msg);
        Svc.Log.Verbose(msg);
    }

    public static void PrintChat(string msg) {
        Status = msg;

        if (Configuration.ShowChatLogs)
            Svc.Chat.Print(msg);
    }
}

public static class NotificationMasterApiExtensions {
    extension(NotificationMasterAPI.NotificationMasterApi api) {
        public bool Notify(NotificationMasterConfig cfg, string toastTitle) {
            if (!cfg.Enabled || !Service.NotificationMaster.IsIPCReady()) return false;

            if (cfg.DisplayToastNotification && Service.NotificationMaster.DisplayTrayNotification(toastTitle, cfg.ToastText)) return true;
            if (cfg.FlashTaskbarIcon && Service.NotificationMaster.FlashTaskbarIcon()) return true;
            if (cfg.BringGameForeground && Service.NotificationMaster.TryBringGameForeground()) return true;
            if (cfg.PlaySound && Service.NotificationMaster.PlaySound(cfg.SoundPath, cfg.SoundVolume, cfg.SoundRepeat, cfg.StopSoundOnceFocused)) return true;

            return false;
        }
    }
}
