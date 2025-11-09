using System.ComponentModel;
using Newtonsoft.Json;

namespace AutoHook.Spearfishing;

public class SpearFishingPresets : BasePreset
{
    public bool AutoGigEnabled = false;
    public bool AutoGigHideOverlay = false;

    [DefaultValue(true)]
    public bool AutoGigDrawFishHitbox = true;

    [DefaultValue(true)]
    public bool AutoGigDrawGigHitbox = true;

    public AutoThaliaksFavor ThaliaksFavor = new(true);

    public bool CatchAll = false;
    public bool CatchAllNaturesBounty = false;

    public bool NatureBountyBeforeFish = false;

    public List<AutoGigConfig> Presets = [];

    [JsonIgnore] public override List<BasePresetConfig> PresetList => [.. Presets.Cast<BasePresetConfig>()];

    [JsonIgnore] public override AutoGigConfig? SelectedPreset => base.SelectedPreset as AutoGigConfig;

    public override void AddNewPreset(string presetName)
    {
        var newPreset = new AutoGigConfig(presetName);
        Presets.Add(newPreset);
        SelectedGuid = newPreset.UniqueId.ToString();
        Service.Save();
    }

    public override void AddNewPreset(BasePresetConfig preset)
    {
        var json = JsonConvert.SerializeObject(preset);
        var copy = JsonConvert.DeserializeObject<AutoGigConfig>(json);
        copy!.UniqueId = Guid.NewGuid();
        Presets.Add(copy);
        SelectedGuid = copy.UniqueId.ToString();
        Service.Save();
    }

    public override void RemovePreset(Guid value)
    {
        var preset = Presets.Find(p => p.UniqueId == value);
        if (preset == null)
            return;

        Presets.Remove(preset);
        Service.Save();
    }

    public override void SwapIndex(int itemIndex, int targetIndex)
    {
        var moved = Presets[itemIndex];

        if (moved == null)
            return;

        RemovePreset(moved.UniqueId);
        Presets.Insert(targetIndex, moved);
        Service.Save();
    }
}