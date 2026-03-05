using Dalamud.Configuration;
using AutoHook.Conditions;
using Newtonsoft.Json;
using System.ComponentModel;
using System.IO.Compression;
using System.IO;
using AutoHook.Configurations.old_config;
using AutoHook.Spearfishing;

namespace AutoHook.Configurations;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 6;
    public string CurrentLanguage { get; set; } = @"en";

    public bool HideLocButtonn = true;

    [DefaultValue(true)] public bool PluginEnabled = true;

    public FishingPresets HookPresets = new();

    public SpearFishingPresets AutoGigConfig = new();

    public bool ShowDebugConsole = false;

    [DefaultValue(true)] public bool ShowChatLogs = true;

    public int DelayBetweenCastsMin = 600;
    public int DelayBetweenCastsMax = 1000;

    public int DelayBetweenHookMin = 100;
    public int DelayBetweenHookMax = 200;

    public int DelayBeforeCancelMin = 1500;
    public int DelayBeforeCancelMax = 2000;

    [DefaultValue(true)] public bool ShowStatus = true;
    public bool ShowPresetsAsSidebar = false;

    public bool HideTabDescription = false;

    public bool SwapToButtons = false;
    public int SwapType;

    [DefaultValue(true)] public bool DontHideOptionsDisabled = true;

    [DefaultValue(true)] public bool ResetAfkTimer = true;

    [DefaultValue(false)] public bool AutoStartFishing = false;

    [DefaultValue(false)] public bool DtrBarEnabled = false;

    [DefaultValue(false)] public bool DtrPresetBarEnabled = false;

    // old config
    public List<BaitPresetConfig> BaitPresetList = []; // legacy

    public void Save()
    {
        Svc.PluginInterface!.SavePluginConfig(this);
    }

    public void UpdateVersion()
    {
        if (Version == 1)
        {
            Version = 2;
        }

        if (Version == 2)
        {
            try
            {
                foreach (var preset in BaitPresetList)
                {
                    var newPreset = ConvertOldPreset(preset);
                    if (newPreset != null)
                        HookPresets.CustomPresets.Add(newPreset);
                }

                Version = 3;
            }
            catch (Exception e)
            {
                Service.PrintDebug(@$"[Configuration] {e.Message}");
            }
        }

        if (Version == 3)
        {
            Service.PrintDebug(@$"[Configuration] Updating to v4");

            Save();
            Version = 4;
        }

        if (Version == 4)
        {
            Service.PrintDebug(@$"[Configuration] Updating to v5");

            foreach (var gig in AutoGigConfig.Presets)
            {
                Service.PrintDebug($"Renaming {gig.PresetName} to {gig.Name}");
                gig.PresetName = gig.Name;
            }

            HookPresets.DefaultPreset.PresetName = Service.GlobalPresetName;

            Save();
            Version = 5;
        }

        if (Version == 5)
        {
            Service.PrintDebug(@$"[Configuration] Updating to v6");

            try
            {
                MigrateConditionsToConditionSets();
                MigrateExtraToTriggers();
            }
            catch (Exception e)
            {
                Service.PrintDebug(@$"[Configuration] v6 migration failed: {e.Message}");
            }

            Save();
            Version = 6;
        }
    }

    private static void SetFieldNewClass(HookConfig newOne, BaitConfig old)
    {
        var oldType = old.GetType();
        var newType = newOne.GetType();

        var oldFields = oldType.GetFields();
        var newFields = newType.GetFields();

        foreach (var sourceField in oldFields)
        {
            var targetField =
                newFields.FirstOrDefault(f => f.Name == sourceField.Name && f.FieldType == sourceField.FieldType);
            if (targetField != null)
            {
                var value = sourceField.GetValue(old);
                targetField.SetValue(newOne, value);
            }
        }
    }

    /// <summary>
    /// v6 migration: populate ConditionSet-backed fields from legacy flags/timers.
    /// This keeps old fields for backward compat while enabling the new condition engine.
    /// </summary>
    private void MigrateConditionsToConditionSets()
    {
        void MigratePreset(CustomPresetConfig preset)
        {
            foreach (var hook in preset.ListOfBaits)
            {
                MigrateHookConfig(hook);
            }

            foreach (var hook in preset.ListOfMooch)
            {
                MigrateHookConfig(hook);
            }

            foreach (var fish in preset.ListOfFish)
            {
                MigrateFishConfig(fish);
            }

            MigrateAutoCordial(preset.AutoCastsCfg.CastCordial);
            MigrateAutoIdenticalCast(preset.AutoCastsCfg.CastIdenticalCast);
        }

        MigratePreset(HookPresets.DefaultPreset);
        foreach (var preset in HookPresets.CustomPresets)
            MigratePreset(preset);
    }

    /// <summary>
    /// v6 migration: convert legacy ExtraConfig flags (intuition, spectral, angler's art, swimbait)
    /// into generic trigger-based ExtraConfig.Triggers using ConditionSets.
    /// </summary>
    private void MigrateExtraToTriggers()
    {
        void MigratePreset(CustomPresetConfig preset)
        {
            var extra = preset.ExtraCfg;
            if (extra == null)
                return;

            // Intuition gained
            if ((extra.SwapPresetIntuitionGain || extra.SwapBaitIntuitionGain) && extra.Triggers.Count < 16)
            {
                var set = new ConditionSet
                {
                    CombineMode = ConditionCombineMode.All,
                    Groups =
                    [
                        new()
                        {
                            CombineMode = ConditionCombineMode.All,
                            Conditions =
                            [
                                new()
                                {
                                    TypeId = ConditionId.IntuitionActive,
                                    Params = []
                                }
                            ]
                        }
                    ]
                };

                extra.Triggers.Add(new ExtraTrigger
                {
                    ConditionSet = set,
                    SwapPreset = extra.SwapPresetIntuitionGain,
                    PresetToSwap = extra.PresetToSwapIntuitionGain,
                    SwapBait = extra.SwapBaitIntuitionGain,
                    BaitToSwap = extra.BaitToSwapIntuitionGain,
                    StopAction = ExtraStopAction.None,
                });
            }

            // Intuition lost
            if ((extra.SwapPresetIntuitionLost || extra.SwapBaitIntuitionLost || extra.QuitOnIntuitionLost || extra.StopOnIntuitionLost) && extra.Triggers.Count < 16)
            {
                var set = new ConditionSet
                {
                    CombineMode = ConditionCombineMode.All,
                    Groups =
                    [
                        new()
                        {
                            CombineMode = ConditionCombineMode.All,
                            Conditions =
                            [
                                new()
                                {
                                    TypeId = ConditionId.IntuitionActive,
                                    Params = []
                                }
                            ]
                        }
                    ]
                };

                var stop = ExtraStopAction.None;
                if (extra.QuitOnIntuitionLost)
                    stop = ExtraStopAction.QuitFishing;
                else if (extra.StopOnIntuitionLost)
                    stop = ExtraStopAction.StopOnly;

                // OnLose Intuition = (NOT IntuitionActive)
                set.Groups[0].Conditions[0].Params["inv"] = true;

                extra.Triggers.Add(new ExtraTrigger
                {
                    ConditionSet = set,
                    SwapPreset = extra.SwapPresetIntuitionLost,
                    PresetToSwap = extra.PresetToSwapIntuitionLost,
                    SwapBait = extra.SwapBaitIntuitionLost,
                    BaitToSwap = extra.BaitToSwapIntuitionLost,
                    StopAction = stop,
                });
            }

            // Spectral gained
            if ((extra.SwapPresetSpectralCurrentGain || extra.SwapBaitSpectralCurrentGain) && extra.Triggers.Count < 16)
            {
                var set = new ConditionSet
                {
                    CombineMode = ConditionCombineMode.All,
                    Groups =
                    [
                        new()
                        {
                            CombineMode = ConditionCombineMode.All,
                            Conditions =
                            [
                                new()
                                {
                                    TypeId = ConditionId.SpectralActive,
                                    Params = []
                                }
                            ]
                        }
                    ]
                };

                extra.Triggers.Add(new ExtraTrigger
                {
                    ConditionSet = set,
                    SwapPreset = extra.SwapPresetSpectralCurrentGain,
                    PresetToSwap = extra.PresetToSwapSpectralCurrentGain,
                    SwapBait = extra.SwapBaitSpectralCurrentGain,
                    BaitToSwap = extra.BaitToSwapSpectralCurrentGain,
                    StopAction = ExtraStopAction.None,
                });
            }

            // Spectral lost
            if ((extra.SwapPresetSpectralCurrentLost || extra.SwapBaitSpectralCurrentLost) && extra.Triggers.Count < 16)
            {
                var set = new ConditionSet
                {
                    CombineMode = ConditionCombineMode.All,
                    Groups =
                    [
                        new()
                        {
                            CombineMode = ConditionCombineMode.All,
                            Conditions =
                            [
                                new()
                                {
                                    TypeId = ConditionId.SpectralActive,
                                    Params = []
                                }
                            ]
                        }
                    ]
                };

                // OnLose Spectral = (NOT SpectralActive)
                set.Groups[0].Conditions[0].Params["inv"] = true;

                extra.Triggers.Add(new ExtraTrigger
                {
                    ConditionSet = set,
                    SwapPreset = extra.SwapPresetSpectralCurrentLost,
                    PresetToSwap = extra.PresetToSwapSpectralCurrentLost,
                    SwapBait = extra.SwapBaitSpectralCurrentLost,
                    BaitToSwap = extra.BaitToSwapSpectralCurrentLost,
                    StopAction = ExtraStopAction.None,
                });
            }

            // Angler's Art stacks reached
            if ((extra.SwapPresetAnglersArt || extra.SwapBaitAnglersArt || extra.StopAfterAnglersArt) && extra.AnglerStackQtd > 0 && extra.Triggers.Count < 16)
            {
                var set = new ConditionSet
                {
                    CombineMode = ConditionCombineMode.All,
                    Groups =
                    [
                        new()
                        {
                            CombineMode = ConditionCombineMode.All,
                            Conditions =
                            [
                                new()
                                {
                                    TypeId = ConditionId.StatusStacks,
                                    Params = new Dictionary<string, object>
                                    {
                                        ["ids"] = new List<object> { (long)IDs.Status.AnglersArt },
                                        ["minStacks"] = extra.AnglerStackQtd,
                                    }
                                }
                            ]
                        }
                    ]
                };

                var stop = ExtraStopAction.None;
                if (extra.StopAfterAnglersArt)
                {
                    stop = extra.AnglerStopFishingStep == FishingSteps.Quitting
                        ? ExtraStopAction.QuitFishing
                        : ExtraStopAction.StopOnly;
                }

                extra.Triggers.Add(new ExtraTrigger
                {
                    ConditionSet = set,
                    SwapPreset = extra.SwapPresetAnglersArt,
                    PresetToSwap = extra.PresetToSwapAnglersArt,
                    SwapBait = extra.SwapBaitAnglersArt,
                    BaitToSwap = extra.BaitToSwapAnglersArt,
                    StopAction = stop,
                });
            }

            // Swimbait fills
            if (extra.SwimbaitFillsAction != SwimbaitAction.None && extra.Triggers.Count < 16)
            {
                var set = new ConditionSet
                {
                    CombineMode = ConditionCombineMode.All,
                    Groups =
                    [
                        new()
                        {
                            CombineMode = ConditionCombineMode.All,
                            Conditions =
                            [
                                new()
                                {
                                    TypeId = ConditionId.SwimbaitCount,
                                    Params = new Dictionary<string, object>
                                    {
                                        ["val"] = 3,
                                        ["above"] = true,
                                    }
                                }
                            ]
                        }
                    ]
                };

                var stop = extra.SwimbaitFillsAction == SwimbaitAction.Stop
                    ? ExtraStopAction.StopOnly
                    : ExtraStopAction.None;

                extra.Triggers.Add(new ExtraTrigger
                {
                    ConditionSet = set,
                    SwapPreset = extra.SwimbaitFillsAction == SwimbaitAction.SwapPreset,
                    PresetToSwap = extra.PresetToSwapSwimbaitFills,
                    SwapBait = false,
                    StopAction = stop,
                });
            }

            // Swimbait runs out
            if (extra.SwimbaitRunsOutAction != SwimbaitAction.None && extra.Triggers.Count < 16)
            {
                var set = new ConditionSet
                {
                    CombineMode = ConditionCombineMode.All,
                    Groups =
                    [
                        new()
                        {
                            CombineMode = ConditionCombineMode.All,
                            Conditions =
                            [
                                new()
                                {
                                    TypeId = ConditionId.SwimbaitCount,
                                    Params = new Dictionary<string, object>
                                    {
                                        ["val"] = 0,
                                        ["above"] = false,
                                    }
                                }
                            ]
                        }
                    ]
                };

                var stop = extra.SwimbaitRunsOutAction == SwimbaitAction.Stop
                    ? ExtraStopAction.StopOnly
                    : ExtraStopAction.None;

                extra.Triggers.Add(new ExtraTrigger
                {
                    ConditionSet = set,
                    SwapPreset = extra.SwimbaitRunsOutAction == SwimbaitAction.SwapPreset,
                    PresetToSwap = extra.PresetToSwapSwimbaitRunsOut,
                    SwapBait = false,
                    StopAction = stop,
                });
            }
        }

        MigratePreset(HookPresets.DefaultPreset);
        foreach (var preset in HookPresets.CustomPresets)
            MigratePreset(preset);
    }

    private static void MigrateHookConfig(HookConfig hook)
    {
        if (hook == null) return;
        MigrateHookset(hook.NormalHook);
        MigrateHookset(hook.IntuitionHook);
    }

    private static void MigrateHookset(BaseHookset hookset)
    {
        if (hookset == null) return;

        void MigrateBite(BaseBiteConfig b)
        {
            if (b == null) return;
            // Do not overwrite existing ConditionSets
            if (b.ConditionSet is { Groups.Count: > 0 }) return;

            var set = b.ConditionSet ??= new ConditionSet();
            var group = new ConditionGroup { CombineMode = ConditionCombineMode.All };

            void AddStatus(uint statusId, bool inverse)
            {
                var cond = new Condition
                {
                    TypeId = ConditionId.StatusActive,
                    Params = new Dictionary<string, object>
                    {
                        ["ids"] = new List<object> { (long)statusId }
                    }
                };
                if (inverse)
                    cond.Params["inv"] = true;
                group.Conditions.Add(cond);
            }

            void AddRange(string typeId, double min, double max)
            {
                if (min <= 0 && max <= 0)
                    return;
                var cond = new Condition
                {
                    TypeId = typeId,
                    Params = new Dictionary<string, object>
                    {
                        // "r": [min, max]; max 0 => no upper bound
                        ["r"] = new List<object> { min, max }
                    }
                };
                group.Conditions.Add(cond);
            }

            // Surface Slap
            if (b.OnlyWhenActiveSlap)
                AddStatus(IDs.Status.SurfaceSlap, inverse: false);
            if (b.OnlyWhenNotActiveSlap)
                AddStatus(IDs.Status.SurfaceSlap, inverse: true);

            // Identical Cast
            if (b.OnlyWhenActiveIdentical)
                AddStatus(IDs.Status.IdenticalCast, inverse: false);
            if (b.OnlyWhenNotActiveIdentical)
                AddStatus(IDs.Status.IdenticalCast, inverse: true);

            // Prize Catch
            if (b.PrizeCatchReq)
                AddStatus(IDs.Status.PrizeCatch, inverse: false);
            if (b.PrizeCatchNotReq)
                AddStatus(IDs.Status.PrizeCatch, inverse: true);

            // Multihook: only map the positive case; negative case remains on legacy flag.
            if (b.OnlyWhenActiveMultihook)
            {
                var cond = new Condition
                {
                    TypeId = "MultihookAvailable",
                    Params = []
                };
                group.Conditions.Add(cond);
            }

            // Timers
            if (b.HookTimerEnabled)
                AddRange(ConditionId.BiteTimer, b.MinHookTimer, b.MaxHookTimer);
            if (b.ChumTimerEnabled)
                AddRange(ConditionId.ChumTimer, b.ChumMinHookTimer, b.ChumMaxHookTimer);

            if (group.Conditions.Count > 0)
                set.Groups.Add(group);
        }

        MigrateBite(hookset.TripleWeak);
        MigrateBite(hookset.TripleStrong);
        MigrateBite(hookset.TripleLegendary);
        MigrateBite(hookset.DoubleWeak);
        MigrateBite(hookset.DoubleStrong);
        MigrateBite(hookset.DoubleLegendary);
        MigrateBite(hookset.PatienceWeak);
        MigrateBite(hookset.PatienceStrong);
        MigrateBite(hookset.PatienceLegendary);

        // AutoLures: map its legacy flags into a ConditionSet on the action itself.
        MigrateLures(hookset.CastLures);
    }

    private static void MigrateLures(AutoLures lures)
    {
        if (lures == null) return;
        if (lures.ConditionSet is { Groups.Count: > 0 }) return;

        var set = lures.ConditionSet ??= new ConditionSet();
        var group = new ConditionGroup { CombineMode = ConditionCombineMode.All };

            void AddStatus(uint statusId, bool inverse)
            {
                var cond = new Condition
                {
                    TypeId = ConditionId.StatusActive,
                    Params = new Dictionary<string, object>
                    {
                        ["ids"] = new List<object> { (long)statusId }
                    }
                };
            if (inverse)
                cond.Params["inv"] = true;
            group.Conditions.Add(cond);
        }

        // Surface Slap
        if (lures.OnlyWhenActiveSlap)
            AddStatus(IDs.Status.SurfaceSlap, inverse: false);
        if (lures.OnlyWhenNotActiveSlap)
            AddStatus(IDs.Status.SurfaceSlap, inverse: true);

        // Identical Cast
        if (lures.OnlyWhenActiveIdentical)
            AddStatus(IDs.Status.IdenticalCast, inverse: false);
        if (lures.OnlyWhenNotActiveIdentical)
            AddStatus(IDs.Status.IdenticalCast, inverse: true);

        if (group.Conditions.Count > 0)
            set.Groups.Add(group);
    }

    private static void MigrateFishConfig(FishConfig fish)
    {
        if (fish == null) return;
        if (fish.IgnoreConditionSet is { Groups.Count: > 0 }) return;
        if (!fish.IgnoreOnIntuition) return;

        var set = fish.IgnoreConditionSet ??= new ConditionSet();
        var group = new ConditionGroup { CombineMode = ConditionCombineMode.All };
        var cond = new Condition
        {
            TypeId = ConditionId.IntuitionActive,
            Params = []
        };
        group.Conditions.Add(cond);
        set.Groups.Add(group);
    }

    private static void MigrateAutoCordial(AutoCordial cordial)
    {
        if (cordial == null) return;
        if (cordial.OvercapConditionSet is { Groups.Count: > 0 }) return;
        if (!cordial.AllowOvercapIC) return;

        var set = cordial.OvercapConditionSet ??= new ConditionSet();
        var group = new ConditionGroup { CombineMode = ConditionCombineMode.All };
        var cond = new Condition
        {
            TypeId = ConditionId.StatusActive,
            Params = new Dictionary<string, object>
            {
                ["ids"] = new List<object> { (long)IDs.Status.IdenticalCast }
            }
        };
        group.Conditions.Add(cond);
        set.Groups.Add(group);
    }

    /// <summary>
    /// v7 migration: convert legacy AutoIdenticalCast flags (Patience / Cordial) into ConditionSet-based rules.
    /// After this, the booleans are effectively legacy-only and can be removed from the UI.
    /// </summary>
    private static void MigrateAutoIdenticalCast(AutoIdenticalCast ic)
    {
        if (ic == null) return;
        // If user already configured a ConditionSet, don't touch it.
        if (ic.ConditionSet is { Groups.Count: > 0 }) return;

        var anyLegacy = ic.OnlyUseUnderPatience || ic.OnlyWhenCordialAvailable;
        if (!anyLegacy) return;

        var set = ic.ConditionSet ??= new ConditionSet();

        // Build a single group that ANDs Patience + (any cordial available) if both are set;
        // or just the one that is set.
        var group = new ConditionGroup { CombineMode = ConditionCombineMode.All };

        if (ic.OnlyUseUnderPatience)
        {
            group.Conditions.Add(new Condition
            {
                TypeId = ConditionId.StatusActive,
                Params = new Dictionary<string, object>
                {
                    ["ids"] = new List<object> { (long)IDs.Status.AnglersFortune }
                }
            });
        }

        if (ic.OnlyWhenCordialAvailable)
        {
            // OR of any cordial item being available.
            // Achieve this by a subgroup with CombineMode.Any over multiple ActionAvailable conditions.
            var cordialGroup = new ConditionGroup
            {
                CombineMode = ConditionCombineMode.Any,
                Conditions =
                [
                    new Condition
                    {
                        TypeId = ConditionId.ActionAvailable,
                        Params = new Dictionary<string, object>
                        {
                            ["id"] = (long)IDs.Item.Cordial,
                            ["type"] = 1L, // ActionType.Item
                        }
                    },
                    new Condition
                    {
                        TypeId = ConditionId.ActionAvailable,
                        Params = new Dictionary<string, object>
                        {
                            ["id"] = (long)IDs.Item.HQCordial,
                            ["type"] = 1L,
                        }
                    },
                    new Condition
                    {
                        TypeId = ConditionId.ActionAvailable,
                        Params = new Dictionary<string, object>
                        {
                            ["id"] = (long)IDs.Item.HiCordial,
                            ["type"] = 1L,
                        }
                    },
                    new Condition
                    {
                        TypeId = ConditionId.ActionAvailable,
                        Params = new Dictionary<string, object>
                        {
                            ["id"] = (long)IDs.Item.WateredCordial,
                            ["type"] = 1L,
                        }
                    },
                    new Condition
                    {
                        TypeId = ConditionId.ActionAvailable,
                        Params = new Dictionary<string, object>
                        {
                            ["id"] = (long)IDs.Item.HQWateredCordial,
                            ["type"] = 1L,
                        }
                    },
                ]
            };

            // Represent cordialGroup as a single logical term "C" in Expression by
            // appending it as another group, and using Expression to AND A && C if needed.
            // For simplicity, if we already have a Patience condition, we just AND it with
            // "(B || C || D ...)" by using the Expression over the two groups.
            if (group.Conditions.Count == 0)
            {
                // Only cordial flag set: just use cordialGroup as the sole group.
                set.Groups.Add(cordialGroup);
                return;
            }

            // Both patience + cordial: group index 0 = patience, index 1 = cordialGroup
            set.Groups.Add(group);
            set.Groups.Add(cordialGroup);
            set.Expression = "A && B";
            return;
        }

        if (group.Conditions.Count > 0)
            set.Groups.Add(group);
    }

    public void Initiate()
    {
        if (HookPresets.DefaultPreset.ListOfBaits.Count != 0)
            return;

        var bait = new BaitFishClass(UIStrings.All_Baits, 0);
        var mooch = new BaitFishClass(UIStrings.All_Mooches, 0);

        HookPresets.DefaultPreset.ListOfBaits.Add(new HookConfig(bait));
        HookPresets.DefaultPreset.ListOfMooch.Add(new HookConfig(mooch));
    }

    public static Configuration Load()
    {
        try
        {
            if (Svc.PluginInterface.GetPluginConfig() is Configuration config)
            {
                config.Initiate();
                config.UpdateVersion();
                config.Save();
                return config;
            }

            config = new Configuration();
            config.Initiate();
            config.Save();
            return config;
        }
        catch (Exception e)
        {
            Svc.Log.Error(@$"[Configuration] {e.Message}");
            throw;
        }
    }

    public static void ResetConfig()
    {
    }

    // Got the export/import function from the UnknownX7's ReAction repo
    /*public static string ExportPreset(CustomPresetConfig preset)
    {
        return CompressString(JsonConvert.SerializeObject(preset,
            new JsonSerializerSettings { DefaultValueHandling = DefaultValueHandling.Ignore }));
    }*/

    public static string ExportPreset(BasePresetConfig preset)
    {
        var exported = CompressString(JsonConvert.SerializeObject(
            preset,
            new JsonSerializerSettings { DefaultValueHandling = DefaultValueHandling.Ignore }));

        // check if preset is type of AutoGigConfig or CustomPresetConfig
        if (preset is AutoGigConfig)
            return ExportPrefixSf + exported;
        else if (preset is CustomPresetConfig)
            return ExportPrefixV6 + exported;

        return "Something went wrong while exporting the preset";
    }

    public class FolderExport(string name)
    {
        public string FolderName { get; set; } = name;
        public List<CustomPresetConfig> Presets { get; set; } = [];
    }

    public static string ExportFolder(PresetFolder folder, List<CustomPresetConfig> presets)
    {
        var folderExport = new FolderExport(folder.FolderName);

        foreach (var presetId in folder.PresetIds)
        {
            var preset = presets.FirstOrDefault(p => p.UniqueId == presetId);
            if (preset != null)
            {
                folderExport.Presets.Add(preset);
            }
        }

        var exported = CompressString(JsonConvert.SerializeObject(folderExport,
            new JsonSerializerSettings { DefaultValueHandling = DefaultValueHandling.Ignore }));

        return ExportPrefixFolder + exported;
    }

    public static (PresetFolder Folder, List<CustomPresetConfig> Presets)? ImportFolder(string import)
    {
        if (!import.StartsWith(ExportPrefixFolder))
            return null;

        try
        {
            var folderData = JsonConvert.DeserializeObject<FolderExport>(DecompressString(import),
                new JsonSerializerSettings { ObjectCreationHandling = ObjectCreationHandling.Replace });

            if (folderData == null)
                return null;

            var folder = new PresetFolder(folderData.FolderName);

            // Generate new GUIDs for all presets to avoid conflicts
            foreach (var preset in folderData.Presets)
            {
                preset.UniqueId = Guid.NewGuid();
                folder.AddPreset(preset.UniqueId);
            }

            return (folder, folderData.Presets);
        }
        catch (Exception e)
        {
            Svc.Log.Error($"Failed to import folder: {e.Message}");
            return null;
        }
    }

    public static BasePresetConfig? ImportPreset(string import)
    {
        if (import.StartsWith(ExportPrefixV2))
        {
            var old = JsonConvert.DeserializeObject<BaitPresetConfig>(DecompressString(import),
                new JsonSerializerSettings() { ObjectCreationHandling = ObjectCreationHandling.Replace });
            return ConvertOldPreset(old);
        }

        if (import.StartsWith(ExportPrefixV3))
        {
            var old = JsonConvert.DeserializeObject<OldPresetConfig>(DecompressString(import),
                new JsonSerializerSettings() { ObjectCreationHandling = ObjectCreationHandling.Replace });

            return ConvertOldPresetV3(old);
        }

        if (import.StartsWith(ExportPrefixSf))
        {
            var autogig = JsonConvert.DeserializeObject<AutoGigConfig>(DecompressString(import),
                new JsonSerializerSettings() { ObjectCreationHandling = ObjectCreationHandling.Replace });

            return autogig;
        }

        var importActionStack = JsonConvert.DeserializeObject<CustomPresetConfig>(DecompressString(import),
            new JsonSerializerSettings() { ObjectCreationHandling = ObjectCreationHandling.Replace });
        return importActionStack;
    }

    [NonSerialized] public static string ExportPrefixV2 = "AH_";
    [NonSerialized] public static string ExportPrefixV3 = "AH3_";
    [NonSerialized] public static string ExportPrefixV4 = "AH4_";
    [NonSerialized] public static string ExportPrefixV6 = "AH6_";
    [NonSerialized] public static string ExportPrefixSf = "AHSF1_";
    [NonSerialized] public static string ExportPrefixFolder = "AHFOLDER_";

    [NonSerialized]
    private static readonly List<string> ExportPrefixes =
    [
        ExportPrefixV2,
        ExportPrefixV3,
        ExportPrefixV4,
        ExportPrefixV6,
        ExportPrefixSf,
        ExportPrefixFolder
    ];

    public static string CompressString(string s)
    {
        var bytes = Encoding.UTF8.GetBytes(s);
        using var ms = new MemoryStream();
        using (var gs = new GZipStream(ms, CompressionMode.Compress))
            gs.Write(bytes, 0, bytes.Length);

        return Convert.ToBase64String(ms.ToArray());
    }

    public static string DecompressString(string s)
    {
        if (!ExportPrefixes.Any(s.StartsWith))
            throw new ApplicationException(UIStrings.DecompressString_Invalid_Import);

        var prefix = ExportPrefixes.First(s.StartsWith);
        var data = Convert.FromBase64String(s[prefix.Length..]);
        var lengthBuffer = new byte[4];
        Array.Copy(data, data.Length - 4, lengthBuffer, 0, 4);
        var uncompressedSize = BitConverter.ToInt32(lengthBuffer, 0);

        var buffer = new byte[uncompressedSize];
        using (var ms = new MemoryStream(data))
        {
            using var gzip = new GZipStream(ms, CompressionMode.Decompress);
            gzip.ReadExactly(buffer, 0, uncompressedSize);
        }

        return Encoding.UTF8.GetString(buffer);
    }

    public static string DecompressBase64(string base64)
    {
        try
        {
            var bytes = Convert.FromBase64String(base64);
            using var compressedStream = new MemoryStream(bytes);
            using var zipStream = new GZipStream(compressedStream, CompressionMode.Decompress);
            using var resultStream = new MemoryStream();
            zipStream.CopyTo(resultStream);
            bytes = resultStream.ToArray();
            return Encoding.UTF8.GetString(bytes, 1, bytes.Length - 1);
        }
        catch (Exception e)
        {
            Svc.Log.Error(@$"Failed to DecompressBase64: {e.Message}");
            return "";
        }
    }

    private static CustomPresetConfig? ConvertOldPreset(BaitPresetConfig? preset)
    {
        if (preset == null)
            return null;

        var filteredBaits = new List<HookConfig>();
        var filteredMooch = new List<HookConfig>();
        foreach (var old in preset.ListOfBaits)
        {
            var matchingBait = GameRes.Baits.FirstOrDefault(b => b.Name == old.BaitName);
            var matchingFish = GameRes.Fishes.FirstOrDefault(f => f.Name == old.BaitName);

            if (matchingBait != null)
            {
                var newOne = new HookConfig(matchingBait);
                SetFieldNewClass(newOne, old);
                filteredBaits.Add(newOne);
            }
            else if (matchingFish != null)
            {
                var newOne = new HookConfig(matchingFish);
                SetFieldNewClass(newOne, old);
                filteredMooch.Add(newOne);
            }
        }

        CustomPresetConfig newPreset = new(@$"[Old Version] {preset.PresetName}")
        {
            ListOfBaits = filteredBaits,
            ListOfMooch = filteredMooch
        };
        return newPreset;
    }

    private static CustomPresetConfig? ConvertOldPresetV3(OldPresetConfig? old)
    {
        if (old == null)
            return null;

        var newPreset = new CustomPresetConfig(old.PresetName);

        Service.PrintDebug($"Converting v3 to v4: {old.PresetName}");
        foreach (var bait in old.ListOfBaits)
        {
            bait.ConvertV3ToV4();

            var newBait = new HookConfig(bait.BaitFish)
            {
                Enabled = bait.Enabled,
                NormalHook = bait.NormalHook,
                IntuitionHook = bait.IntuitionHook
            };
            newBait.IntuitionHook.UseCustomStatusHook = bait.UseCustomIntuitionHook;

            newPreset.AddItem(newBait);
        }

        foreach (var mooch in old.ListOfMooch)
        {
            mooch.ConvertV3ToV4();
            var newMooch = new HookConfig(mooch.BaitFish)
            {
                Enabled = mooch.Enabled,
                NormalHook = mooch.NormalHook,
                IntuitionHook = mooch.IntuitionHook
            };
            newMooch.IntuitionHook.UseCustomStatusHook = mooch.UseCustomIntuitionHook;

            newPreset.AddItem(newMooch);
        }

        newPreset.ListOfFish = old.ListOfFish;
        newPreset.ExtraCfg = old.ExtraCfg;
        newPreset.AutoCastsCfg = old.AutoCastsCfg;

        return newPreset;
    }
}
