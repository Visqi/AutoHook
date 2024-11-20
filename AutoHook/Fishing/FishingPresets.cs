﻿using System;
using System.Collections.Generic;
using System.Linq;
using AutoHook.Classes;
using AutoHook.Configurations;
using Newtonsoft.Json;

namespace AutoHook.Fishing;

public class FishingPresets : BasePreset
{
    // Global preset, cant rename rn 
    public CustomPresetConfig DefaultPreset = new(Service.GlobalPresetName);

    public List<CustomPresetConfig> CustomPresets = new();

    [JsonIgnore] public override CustomPresetConfig? SelectedPreset => base.SelectedPreset as CustomPresetConfig;

    public override void AddNewPreset(string presetName)
    {
        var newPreset = new CustomPresetConfig(presetName);
        CustomPresets.Add(newPreset);
        Service.Save();
    }
    
    public override void AddNewPreset(BasePresetConfig preset) 
    {
        // i needed a way to copy the object without reference, im too dumb to think of another way
        var json = JsonConvert.SerializeObject(preset); 
        var copy = JsonConvert.DeserializeObject<CustomPresetConfig>(json);
        copy!.UniqueId = Guid.NewGuid();
        CustomPresets.Add(copy);
        Service.Save();
    }

    public override void RemovePreset(Guid value)
    {
        var preset = CustomPresets.Find(p => p.UniqueId == value);
        if (preset == null)
            return;

        CustomPresets.Remove(preset);
        Service.Save();
    }

    public override void OnSelectedPreset(BasePresetConfig newPreset, BasePresetConfig? oldPreset)
    {
        if (oldPreset is not CustomPresetConfig old)
            return;

        if (old is { ExtraCfg: { Enabled: true, ResetCounterPresetSwap: true } })
            old.ResetCounter();
        
        Service.Save();
    }

    public override void SwapIndex(int itemIndex, int targetIndex)
    {
        var moved = CustomPresets[itemIndex];
        
        if(moved == null)
            return;
        
        RemovePreset(moved.UniqueId);
        CustomPresets.Insert(targetIndex, moved);
        Service.Save();
    }


    [JsonIgnore] public override List<BasePresetConfig> PresetList => CustomPresets.Cast<BasePresetConfig>().ToList();
}