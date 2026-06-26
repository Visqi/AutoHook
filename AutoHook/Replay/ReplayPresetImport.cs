using Newtonsoft.Json;

namespace AutoHook.Replay;

public static class ReplayPresetImport {
    public static bool TryImport(FishingReplay replay, out string? error) {
        error = null;
        var json = replay.Metadata.PresetSnapshotJson;
        if (string.IsNullOrWhiteSpace(json)) {
            error = "This replay has no preset snapshot.";
            return false;
        }

        try {
            json = ConfigurationJsonMigrator.MigrateImportedPreset(json);
            var preset = JsonConvert.DeserializeObject<CustomPresetConfig>(json);
            if (preset == null) {
                error = "Failed to deserialize preset snapshot.";
                return false;
            }

            preset.UniqueId = Guid.NewGuid();
            var baseName = preset.PresetName == Service.GlobalPresetName ? $"{preset.PresetName} (replay)" : preset.PresetName;
            preset.RenamePreset(UniquePresetName(baseName));

            var hooks = Service.Configuration.HookPresets;
            hooks.AddNewPreset(preset);
            hooks.SelectedPreset = preset;
            Service.Save();
            return true;
        }
        catch (Exception e) {
            error = e.Message;
            return false;
        }
    }

    private static string UniquePresetName(string baseName) {
        var presets = Service.Configuration.HookPresets.CustomPresets;
        if (presets.All(p => p.PresetName != baseName))
            return baseName;

        for (var i = 2; ; i++) {
            var candidate = $"{baseName} ({i})";
            if (presets.All(p => p.PresetName != candidate))
                return candidate;
        }
    }
}
