using clib.Configuration;
using Newtonsoft.Json.Linq;

namespace AutoHook.Presets.Migration;

file sealed class ConfigJsonV3 : IConfigJsonMigration<Configuration> {
    public int TargetVersion => 3;
    public void Migrate(JObject root) => ConfigurationJsonMigrator.MigrateV2ToV3Json(root);
}

file sealed class ConfigJsonV5 : IConfigJsonMigration<Configuration> {
    public int TargetVersion => 5;

    public void Migrate(JObject root) {
        var migrated = ConfigurationJsonMigrator.RunRuntimeMigrationsUpTo5(root);
        var next = JObject.Parse(migrated);
        root.RemoveAll();
        foreach (var prop in next.Properties())
            root[prop.Name] = prop.Value;
    }
}

file sealed class ConfigJsonV6 : IConfigJsonMigration<Configuration> {
    public int TargetVersion => 6;
    public void Migrate(JObject root) => ConfigurationJsonMigrator.MigrateV6(root);
}

file sealed class ConfigJsonV7 : IConfigJsonMigration<Configuration> {
    public int TargetVersion => 7;
    public void Migrate(JObject root) => ConfigurationJsonMigrator.MigrateV7(root);
}

file sealed class ConfigJsonV8 : IConfigJsonMigration<Configuration> {
    public int TargetVersion => 8;
    public void Migrate(JObject root) => ConfigurationJsonMigrator.MigrateV8(root);
}

file sealed class ConfigJsonV9 : IConfigJsonMigration<Configuration> {
    public int TargetVersion => 9;
    public void Migrate(JObject root) => ConfigurationJsonMigrator.MigrateV9(root);
}

file sealed class ConfigJsonV10 : IConfigJsonMigration<Configuration> {
    public int TargetVersion => 10;
    public void Migrate(JObject root) => ConfigurationJsonMigrator.MigrateV10(root);
}

file sealed class ConfigJsonV11 : IConfigJsonMigration<Configuration> {
    public int TargetVersion => 11;
    public void Migrate(JObject root) => ConfigurationJsonMigrator.MigrateV11(root);
}
