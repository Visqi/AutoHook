using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

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
            if (JObject.Parse(json)["Gigs"] is not null)
                return TryImportSpearfishing(json, out error);

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

    private static bool TryImportSpearfishing(string json, out string? error) {
        json = ConfigurationJsonMigrator.MigrateImportedSpearfishingPreset(json);
        var preset = JsonConvert.DeserializeObject<AutoGigConfig>(json);
        if (preset == null) {
            error = "Failed to deserialize spearfishing preset snapshot.";
            return false;
        }

        preset.UniqueId = Guid.NewGuid();
        preset.RegenerateNestedUniqueIds();
        preset.RenamePreset(UniqueSpearfishingPresetName(preset.PresetName));
        Service.Configuration.AutoGigConfig.AddNewPreset(preset);
        Service.Configuration.AutoGigConfig.SelectedPreset = preset;
        Service.Save();
        error = null;
        return true;
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

    private static string UniqueSpearfishingPresetName(string baseName) {
        var presets = Service.Configuration.AutoGigConfig.Presets;
        if (presets.All(p => p.PresetName != baseName))
            return baseName;

        for (var i = 2; ; i++) {
            var candidate = $"{baseName} ({i})";
            if (presets.All(p => p.PresetName != candidate))
                return candidate;
        }
    }
}
