using AutoHook.IPC;
using AutoHook.Spearfishing;
using clib;
using Dalamud.Game.Command;
using Dalamud.Game.Gui.Dtr;
using Dalamud.Game.Text;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using ECommons.EzDTR;
using PunishLib;
using System.Globalization;

namespace AutoHook;

/* 
 * TODO: 
 * get rid of "don't cancel mooch" configs under per fish surface slap/IC
 * get rid of all other configs that could be conditions in auto casts et al. Migrate them to conditions.
 * adding/changing a condition should default the tree node to be uncollapsed e.g. open
 * under each swap/stop rule, add the ability to reset counters
 * show complex condition ui by default if there are multiple groups
 * stop movement while fishing
 * auto extract materia
 * auto reduce fish
 * move around to reduce fish weary
 * BUGS:
 * if you change preset and tell it to use swimbait it uses the normal baits timeout options for the first one. this seems to happen regardless if you have a timer on...
 * resolve collectables window under swap/stop rules doesn't seem to be doing anything when checked. If it's checked but force no isn't, we treat that as resolve as it would normally if the global option was checked
 * start fishing rule doesn't seem to work?
 * mutltihook is checking gp for triple hook/double hook? idk what this report was about
 *
 * the rule success (condition: fisher's int || status: surface slap) -> swap presets doesn't seem to work
 * force resolve collectables doesn't seem to do anything. The general one works but not the force setting
 */

public class AutoHook : IDalamudPlugin {
    public string Name => UIStrings.AutoHook;

    internal static AutoHook Plugin = null!;

    //todo: - Spearfishing rework
    private const string CmdAhCfg = "/ahcfg";
    private const string CmdAh = "/autohook";
    private const string CmdAhOn = "/ahon";
    private const string CmdAhOff = "/ahoff";
    private const string CmdAhtg = "/ahtg";
    private const string CmdAhPreset = "/ahpreset";
    private const string CmdAhStart = "/ahstart";
    private const string CmdAhBait = "/ahbait";
    private const string CmdBait = "/bait";
    private const string CmdAgPreset = "/agpreset";

    private static readonly Dictionary<string, string> CommandHelp = new()
    {
        { CmdAhOff, UIStrings.Disables_AutoHook },
        { CmdAhOn, UIStrings.Enables_AutoHook },
        { CmdAhCfg, UIStrings.Opens_Config_Window },
        { CmdAh, UIStrings.Opens_Config_Window },
        { CmdAhtg, UIStrings.Toggles_AutoHook_On_Off },
        { CmdAhPreset, UIStrings.Set_preset_command },
        { CmdAhStart, UIStrings.Starts_AutoHook },
        { CmdAhBait, UIStrings.SwitchFishBait },
        { CmdBait, UIStrings.SwitchFishBait },
        { CmdAgPreset, UIStrings.Set_agpreset_command }
    };

    private static PluginUi _pluginUi = null!;
    private static AutoGig _autoGig = null!;
    public readonly FishingManager HookManager;
    public AutoHookIPC AutoHookIpc;

    public AutoHook(IDalamudPluginInterface pluginInterface, IDtrBar dtrBar) {
        ECommonsMain.Init(pluginInterface, this, Module.DalamudReflector, Module.ObjectFunctions);
        CLibMain.Init(pluginInterface, this);
        Service.Initialize(pluginInterface);
        PunishLibMain.Init(pluginInterface, "AutoHook", new AboutPlugin() { Developer = "InitialDet", Sponsor = "https://ko-fi.com/initialdet" });

        Plugin = this;

        Service.WorldState = new WorldState();
        Service.Configuration = Configuration.Load();
        UIStrings.Culture = new CultureInfo(Service.Configuration.CurrentLanguage);
        Service.AutoCollectables = new AutoCollectables();
        Service.NotificationMaster = new(pluginInterface);
        Service.WorldStateUpdater = new WorldStateUpdater();

        _pluginUi = new PluginUi();
        _autoGig = new AutoGig();
        HookManager = new FishingManager();
        AutoHookIpc = new AutoHookIPC();

        foreach (var (command, help) in CommandHelp) {
            Svc.Commands.AddHandler(command, new CommandInfo(OnCommand) {
                HelpMessage = help
            });
        }

        GameRes.Initialize();

        Svc.PluginInterface.UiBuilder.Draw += Service.WindowSystem.Draw;
        Svc.PluginInterface.UiBuilder.OpenConfigUi += _pluginUi.Toggle;
        Svc.PluginInterface.UiBuilder.OpenMainUi += _pluginUi.Toggle;

        _ = new EzDtr(() =>
            $"{((SeIconChar)0xE05E).ToIconString()} {(Service.Configuration.PluginEnabled ? UIStrings.Enabled : UIStrings.Disabled)}",
            evt => {
                if (evt.ClickType is MouseClickType.Left) {
                    Service.Configuration.PluginEnabled ^= true;
                    Service.Configuration.Save();
                }
                else if (evt.ClickType is MouseClickType.Right)
                    _pluginUi.Toggle();
            },
            showCondition: () => Service.Configuration.DtrBarEnabled && Player.Job is ECommons.ExcelServices.Job.FSH
        );

        _ = new EzDtr(() => $"{SeIconChar.Collectible.ToIconString()} {Service.Configuration.HookPresets.SelectedPreset?.PresetName ?? $"{UIStrings.GlobalPreset}"}",
            evt => {
                if (Service.Configuration.HookPresets.SelectedPreset == null) return;
                var presets = Service.Configuration.HookPresets.CustomPresets;
                var index = presets.IndexOf(Service.Configuration.HookPresets.SelectedPreset);
                var direction = evt.ClickType == MouseClickType.Left ? 1 : -1;
                Service.Configuration.HookPresets.SelectedPreset = presets[(index + direction + presets.Count) % presets.Count];
                Service.Configuration.Save();
            },
            $"{Name}Presets",
            () => Service.Configuration.DtrPresetBarEnabled && Player.Job is ECommons.ExcelServices.Job.FSH && Service.Configuration.HookPresets.SelectedPreset != null
        );

#if DEBUG
        if (Svc.ClientState.IsLoggedIn)
            _pluginUi.Toggle();
#endif
    }

    private void OnCommand(string command, string args) {
        switch (command.Trim()) {
            case CmdAhCfg:
            case CmdAh:
                _pluginUi.Toggle();
                break;
            case CmdAhOn:
                Svc.Chat.Print(UIStrings.AutoHook_Enabled);
                Service.Configuration.PluginEnabled = true;
                break;
            case CmdAhOff:
                Svc.Chat.Print(UIStrings.AutoHook_Disabled);
                Service.Configuration.PluginEnabled = false;
                break;
            case CmdAhtg when Service.Configuration.PluginEnabled:
                Svc.Chat.Print(UIStrings.AutoHook_Disabled);
                Service.Configuration.PluginEnabled = false;
                break;
            case CmdAhtg:
                Svc.Chat.Print(UIStrings.AutoHook_Enabled);
                Service.Configuration.PluginEnabled = true;
                break;
            case CmdAhPreset:
                SetPreset(args);
                break;
            case CmdAhStart:
                HookManager.StartFishing();
                break;
            case CmdBait:
            case CmdAhBait:
                SwapBait(args);
                break;
            case CmdAgPreset:
                SetGigPreset(args);
                break;
        }
    }

    private static void SwapBait(string args) {
        var bait = GameRes.Baits.FirstOrDefault(f => f.Name.ToLower() == args.ToLower() || f.Id.ToString() == args);
        FishingManager.ChangeBait((uint)bait?.Id!);
    }

    private static void SetPreset(string presetName) {
        var preset = Service.Configuration.HookPresets.CustomPresets.FirstOrDefault(x => x.PresetName == presetName);
        if (preset == null) {
            Svc.Chat.Print(UIStrings.Preset_not_found);
            return;
        }

        Service.Save();
        Service.Configuration.HookPresets.SelectedPreset = preset;
        Svc.Chat.Print(@$"{UIStrings.Preset_set_to_} {preset.PresetName}");
        Service.Save();
    }

    private static void SetGigPreset(string presetName) {
        try {
            var preset = Service.Configuration.AutoGigConfig.Presets.FirstOrDefault(x => x.PresetName == presetName);
            if (preset == null) {
                Svc.Chat.Print(@$"{UIStrings.Preset_not_found} - {presetName}");
                return;
            }

            Service.Save();
            Service.Configuration.AutoGigConfig.SelectedPreset = preset;
            Svc.Chat.Print(@$"{UIStrings.Gig_preset_set_to_} {preset.PresetName}");
            Service.Save();
        }
        catch (Exception e) {
            Svc.Log.Error(e.Message);
        }
    }

    public void Dispose() {
        _pluginUi.Dispose();
        _autoGig.Dispose();
        HookManager.Dispose();
        Service.Save();
        Service.WorldStateUpdater.Dispose();
        Service.AutoCollectables.Dispose();
        Svc.PluginInterface.UiBuilder.Draw -= Service.WindowSystem.Draw;
        Svc.PluginInterface.UiBuilder.OpenConfigUi -= _pluginUi.Toggle;
        Svc.PluginInterface.UiBuilder.OpenMainUi -= _pluginUi.Toggle;

        foreach (var (command, _) in CommandHelp)
            Svc.Commands.RemoveHandler(command);

        CLibMain.Dispose();
        ECommonsMain.Dispose();
    }
}
