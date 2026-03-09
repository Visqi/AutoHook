namespace AutoHook.Configurations.Legacy;

public class BaitPresetConfig {
    /* old config, dont use*/
    public string PresetName { get; set; } = UIStrings.New_Preset;
    public List<BaitConfig> ListOfBaits { get; set; } = [];

    public BaitPresetConfig() {
        ListOfBaits = [];
    }

    public BaitPresetConfig(string presetName) : this() {
        PresetName = presetName;
    }

    public void AddBaitConfig(BaitConfig baitConfig) {
        if (ListOfBaits != null && !ListOfBaits.Contains(baitConfig)) {
            ListOfBaits.Add(baitConfig);
        }
    }

    public void RemoveBaitConfig(BaitConfig baitConfig) {
        if (ListOfBaits != null && ListOfBaits.Contains(baitConfig)) {
            ListOfBaits.Remove(baitConfig);
        }
    }

    // This is just for the conversion of the Config version 1 to version 2
    public void AddListOfHook(List<BaitConfig> listOfBaits) {
        ListOfBaits.AddRange(listOfBaits);
    }

    public override bool Equals(object? obj) {
        return obj is BaitPresetConfig settings &&
               PresetName == settings.PresetName;
    }

    public override int GetHashCode() {
        return HashCode.Combine(PresetName + @"a");
    }

    public void RenamePreset(string name) {
        PresetName = name;
    }
}
