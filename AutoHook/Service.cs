using AutoHook.IPC;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using ECommons.Automation.NeoTaskManager;
using System.Globalization;

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
    public static FishingManager FishManager { get; set; } = null!;
    public static AutoHookIPC Ipc { get; set; } = null!;
    public static NotificationMasterAPI.NotificationMasterApi NotificationMaster { get; set; } = null!;

    public static async ValueTask InitAsync() {
        WorldState = new WorldState();
        Configuration = await Configuration.LoadAsync();
        UIStrings.Culture = new CultureInfo(Configuration.CurrentLanguage);
        AutoCollectables = new AutoCollectables();
        NotificationMaster = new(Svc.Interface);
        WorldStateUpdater = new WorldStateUpdater();
        FishManager = new FishingManager();
        Ipc = new AutoHookIPC();
    }

    public static async ValueTask DisposeAsync() {
        FishManager.Dispose();
        await Configuration.FlushAsync();
        WorldStateUpdater.Dispose();
        AutoCollectables.Dispose();
    }

    public static void Save() => Configuration.Save();

    public static string Status {
        get => _status;
        set => _status = value;
    }

    public static readonly TaskManager TaskManager = new() {
        DefaultConfiguration = { TimeLimitMS = 5000 }
    };

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
        public bool TryNotify(NotificationConfig cfg) {
            if (!cfg.Enabled)
                return false;

            var success = false;

            if (Service.NotificationMaster.IsIPCReady()) {
                if (cfg.DisplayToastNotification && Service.NotificationMaster.DisplayTrayNotification("AutoHook", cfg.ToastText)) {
                    success = true;
                }

                if (cfg.FlashTaskbarIcon && Service.NotificationMaster.FlashTaskbarIcon()) {
                    success = true;
                }

                if (cfg.BringGameForeground && Service.NotificationMaster.TryBringGameForeground()) {
                    success = true;
                }
            }

            if (cfg.BeepOnSuccess) {
                const int frequency = 900;
                const int durationMs = 200;
                const int count = 3;

                for (var i = 0; i < count; i++) {
                    try {
                        Console.Beep(frequency, durationMs);
                    }
                    catch {
                        break;
                    }
                }

                success = true;
            }

            return success;
        }
    }
}
