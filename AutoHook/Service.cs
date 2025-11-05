using AutoHook.Classes;
using Dalamud.Game;
using Dalamud.Plugin;
using Dalamud.Interface.Windowing;
using AutoHook.SeFunctions;
using AutoHook.Configurations;
using ECommons.Automation.NeoTaskManager;

namespace AutoHook;

public class Service
{
    public static void Initialize(IDalamudPluginInterface pluginInterface)
        => pluginInterface.Create<Service>();

    public const string PluginName = "AutoHook";

    public const string GlobalPresetName = "Global Preset";

    public static BaitManager BaitManager { get; set; } = null!;
    public static Configuration Configuration { get; set; } = null!;
    public static WindowSystem WindowSystem { get; } = new(PluginName);
    public static SeTugType TugType { get; set; } = null!;
    public static ClientLanguage Language { get; set; }

    public static string _status = @"";

    public static BaitFishClass LastCatch { get; set; } = new(@"-", -1);

    public static string Status
    {
        get => _status;
        set => _status = value;
    }

    public static readonly TaskManager TaskManager = new()
    {
        DefaultConfiguration = { TimeLimitMS = 5000 }
    };

    public static void Save()
    {
        Configuration.Save();
    }

    private const int MaxLogSize = 50;
    public static Queue<string> LogMessages = new();
    public static bool OpenConsole;
    public static void PrintDebug(string msg)
    {
        if (LogMessages.Count >= MaxLogSize)
        {
            LogMessages.Dequeue();
        }

        LogMessages.Enqueue(msg);
        Svc.Log.Debug(msg);
    }

    public static void PrintVerbose(string msg)
    {
        if (LogMessages.Count >= MaxLogSize)
        {
            LogMessages.Dequeue();
        }

        LogMessages.Enqueue(msg);
        Svc.Log.Verbose(msg);
    }

    public static void PrintChat(string msg)
    {
        Status = msg;

        if (Configuration.ShowChatLogs)
            Svc.Chat.Print(msg);
    }
}
