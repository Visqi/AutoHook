namespace AutoHook.Configurations.old_config;

public class OldPresetConfig(string presetName)
{
    public string PresetName { get; set; } = presetName;

    public List<OldHookConfig> ListOfBaits { get; set; } = [];

    public List<OldHookConfig> ListOfMooch { get; set; } = [];

    public List<FishConfig> ListOfFish { get; set; } = [];

    public AutoCastsConfig AutoCastsCfg = new();

    public ExtraConfig ExtraCfg = new();

    public void ConvertV3ToV4()
    {
        foreach (var item in ListOfBaits)
        {
            item.ConvertV3ToV4();
        }

        foreach (var item in ListOfMooch)
        {
            item.ConvertV3ToV4();
        }
    }
}