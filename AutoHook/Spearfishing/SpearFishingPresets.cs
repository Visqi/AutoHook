using Newtonsoft.Json;

namespace AutoHook.Spearfishing;

public class SpearFishingPresets : BasePreset {
    public bool AutoGigEnabled = false;
    public bool AutoGigHideOverlay = false;

    public bool AutoGigDrawFishHitbox = true;

    public bool AutoGigDrawGigHitbox = true;

    public AutoThaliaksFavor ThaliaksFavor = new(true);

    public bool CatchAll = false;
    public bool CatchAllNaturesBounty = false;

    public bool NatureBountyBeforeFish = false;

    public List<AutoGigConfig> Presets = [];

    [JsonIgnore] private List<BasePresetConfig>? _presetListCache;
    [JsonIgnore] private int _presetListCacheCount = -1;

    [JsonIgnore] public override List<BasePresetConfig> PresetList {
        get {
            if (_presetListCache == null || _presetListCacheCount != Presets.Count) {
                _presetListCache = [.. Presets.Cast<BasePresetConfig>()];
                _presetListCacheCount = Presets.Count;
            }

            return _presetListCache;
        }
    }

    private void InvalidatePresetListCache() {
        _presetListCache = null;
        _presetListCacheCount = -1;
    }

    [JsonIgnore] public override AutoGigConfig? SelectedPreset => base.SelectedPreset as AutoGigConfig;

    public override void AddNewPreset(string presetName) {
        var newPreset = new AutoGigConfig(presetName);
        Presets.Add(newPreset);
        InvalidatePresetListCache();
        SelectedGuid = newPreset.UniqueId.ToString();
        Service.Save();
    }

    public override void AddNewPreset(BasePresetConfig preset) {
        var json = JsonConvert.SerializeObject(preset);
        var copy = JsonConvert.DeserializeObject<AutoGigConfig>(json);
        copy!.UniqueId = Guid.NewGuid();
        Presets.Add(copy);
        InvalidatePresetListCache();
        SelectedGuid = copy.UniqueId.ToString();
        Service.Save();
    }

    public override void RemovePreset(Guid value) {
        var preset = Presets.Find(p => p.UniqueId == value);
        if (preset == null)
            return;

        Presets.Remove(preset);
        InvalidatePresetListCache();
        Service.Save();
    }

    public override void SwapIndex(int itemIndex, int targetIndex) {
        var moved = Presets[itemIndex];

        if (moved == null)
            return;

        RemovePreset(moved.UniqueId);
        Presets.Insert(targetIndex, moved);
        InvalidatePresetListCache();
        Service.Save();
    }
}