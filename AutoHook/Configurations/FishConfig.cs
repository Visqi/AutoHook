﻿using System;
using System.ComponentModel;
using AutoHook.Classes;
using AutoHook.Classes.AutoCasts;
using AutoHook.Enums;
using AutoHook.Resources.Localization;

namespace AutoHook.Configurations;

public class FishConfig
{
    private Guid _uniqueId;
    
    [DefaultValue(true)]
    public bool Enabled = true;
    
    public bool IgnoreOnIntuition = false;

    public BaitFishClass Fish = new();
    
    public bool StopAfterCaught = false;
    public int StopAfterCaughtLimit = 1;
    public bool StopAfterResetCount = false;
    
    public AutoIdenticalCast IdenticalCast = new();
    public AutoSurfaceSlap SurfaceSlap = new();
    public AutoMooch Mooch = new();
    
    public bool SwapBait = false;
    public BaitFishClass BaitToSwap = new();
    public int SwapBaitCount = 1;
    
    public bool SwapPresets = false;
    public string PresetToSwap = "-"; 
    public int SwapPresetCount = 1;
    
    public bool NeverMooch = false;
    
    public bool NeverRelease = false;
    
    public FishingSteps StopFishingStep = FishingSteps.None;
    
    public FishConfig(){}
    
    public FishConfig(BaitFishClass fish)
    {
        Fish = fish;
        // ok this is not the best way, but im tired and it works for now so be nice to me
        Mooch.Name = UIStrings.Always_Mooch; 
        
        _uniqueId = Guid.NewGuid();
    }
    
    public FishConfig(int fishId) 
    { 
        Fish = new BaitFishClass(fishId); 
        _uniqueId = Guid.NewGuid(); 
    }
    
    public Guid GetUniqueId()
    {
        if (_uniqueId == Guid.Empty)
            _uniqueId = Guid.NewGuid();
        
        return _uniqueId;
    }
}