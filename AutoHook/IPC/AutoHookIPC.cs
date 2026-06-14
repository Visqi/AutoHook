using ECommons.EzIpcManager;

namespace AutoHook.IPC;

public class AutoHookIPC {
    private readonly Configuration _cfg = Service.Configuration;

    public AutoHookIPC() {
        EzIPC.Init(this, "AutoHook");
    }

    [EzIPC]
    public void SetPluginState(bool state) {
        WriteConfig(() => _cfg.PluginEnabled = state);
        Service.Save();
    }

    [EzIPC]
    public bool GetPluginState() {
        return _cfg.PluginEnabled;
    }

    [EzIPC]
    public bool GetAutoStartFishing() {
        return _cfg.AutoStartFishing;
    }

    [EzIPC]
    public void SetAutoStartFishing(bool state) {
        WriteConfig(() => _cfg.AutoStartFishing = state);
        Service.Save();
    }

    [EzIPC]
    public void SetAutoGigState(bool state) {
        WriteConfig(() => _cfg.AutoGigConfig.AutoGigEnabled = state);
        Service.Save();
    }

    [EzIPC]
    public void SetPreset(string preset) {
        WriteConfig(() => _cfg.HookPresets.SelectedPreset =
            _cfg.HookPresets.CustomPresets.FirstOrDefault(x => x.PresetName == preset));
        Service.Save();
    }

    public void SetPresetAutogig(string preset) {
        WriteConfig(() => _cfg.AutoGigConfig.SelectedPreset =
            _cfg.AutoGigConfig.Presets.FirstOrDefault(x => x.PresetName == preset));
        Service.Save();
    }

    [EzIPC]
    public void CreateAndSelectAnonymousPreset(string preset) {
        var import = Configuration.ImportPreset(preset);
        if (import == null) return;

        WriteConfig(() => {
            var name = $"anon_{import.PresetName}";
            import.RenamePreset(name);
            _cfg.HookPresets.AddNewPreset(import);
            _cfg.HookPresets.SelectedPreset =
                _cfg.HookPresets.CustomPresets.FirstOrDefault(x => x.PresetName == name);
        });
        Service.Save();
    }

    [EzIPC]
    public void ImportAndSelectPreset(string preset) {
        var import = Configuration.ImportPreset(preset);
        if (import == null) return;

        WriteConfig(() => {
            import.RenamePreset(import.PresetName);

            if (import is CustomPresetConfig customPreset)
                _cfg.HookPresets.AddNewPreset(customPreset);
            else if (import is AutoGigConfig gigPreset)
                _cfg.AutoGigConfig.AddNewPreset(gigPreset);
        });
        Service.Save();
    }

    [EzIPC]
    public void DeleteSelectedPreset() {
        WriteConfig(() => {
            var selected = _cfg.HookPresets.SelectedPreset;
            if (selected == null) return;
            _cfg.HookPresets.RemovePreset(selected.UniqueId);
            _cfg.HookPresets.SelectedPreset = null;
        });
        Service.Save();
    }

    [EzIPC]
    public void DeleteAllAnonymousPresets() {
        WriteConfig(() => _cfg.HookPresets.CustomPresets.RemoveAll(p => p.PresetName.StartsWith("anon_")));
        Service.Save();
    }

    [EzIPC]
    public bool SwapBaitById(uint baitId)
        => FishingManager.ChangeBait(baitId) is FishingManager.ChangeBaitReturn.Success or FishingManager.ChangeBaitReturn.AlreadyEquipped;

    [EzIPC]
    public bool SwapBait(string baitNameOrId) {
        if (string.IsNullOrWhiteSpace(baitNameOrId))
            return false;

        if (uint.TryParse(baitNameOrId, out var parsedId))
            return SwapBaitById(parsedId);

        var bait = GameRes.Baits.FirstOrDefault(b => string.Equals(b.Name, baitNameOrId, StringComparison.OrdinalIgnoreCase));

        if (bait == null || bait.Id <= 0)
            return false;

        return FishingManager.ChangeBait((uint)bait.Id) is FishingManager.ChangeBaitReturn.Success or FishingManager.ChangeBaitReturn.AlreadyEquipped;
    }

    // Swaps the current swimbait slot by index (0,1,2).
    [EzIPC]
    public bool SwapSwimbaitByIndex(byte index)
        => FishingManager.ChangeSwimbait(index) is FishingManager.ChangeBaitReturn.Success or FishingManager.ChangeBaitReturn.AlreadyEquipped;

    private static void WriteConfig(Action action) {
        lock (Configuration.SerializationSync)
            action();
    }
}
