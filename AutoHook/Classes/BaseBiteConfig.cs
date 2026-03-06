using Dalamud.Bindings.ImGui;
using AutoHook.Conditions;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using System.ComponentModel;

namespace AutoHook.Classes;

public class BaseBiteConfig(HookType type)
{
    [DefaultValue(true)]
    public bool HooksetEnabled = true;

    public bool EnableHooksetSwap;

    [Obsolete("Legacy config")] public bool HookTimerEnabled;
    [Obsolete("Legacy config")] public double MinHookTimer;
    [Obsolete("Legacy config")] public double MaxHookTimer;
    [Obsolete("Legacy config")] public bool ChumTimerEnabled;
    [Obsolete("Legacy config")] public double ChumMinHookTimer;
    [Obsolete("Legacy config")] public double ChumMaxHookTimer;
    [Obsolete("Legacy config")] public bool OnlyWhenActiveSlap;
    [Obsolete("Legacy config")] public bool OnlyWhenNotActiveSlap;

    public bool OnlyWhenActiveIdentical;
    public bool OnlyWhenNotActiveIdentical;

    public bool PrizeCatchReq;
    public bool PrizeCatchNotReq;

    public bool OnlyWhenActiveMultihook;
    public bool OnlyWhenNotActiveMultihook;

    public ConditionSet? ConditionSet { get; set; }

    public HookType HooksetType = type;

    public bool UseMultipleHookTypesByTimer;

    public bool UseNormalHookTypeByTimer;
    public double NormalHookTypeMin;
    public double NormalHookTypeMax;

    public bool UsePrecisionHookTypeByTimer;
    public double PrecisionHookTypeMin;
    public double PrecisionHookTypeMax;

    public bool UsePowerfulHookTypeByTimer;
    public double PowerfulHookTypeMin;
    public double PowerfulHookTypeMax;

    public bool UseStellarHookTypeByTimer;
    public double StellarHookTypeMin;
    public double StellarHookTypeMax;

    public void DrawOptions(string biteName, bool enableSwap = false)
    {
        EnableHooksetSwap = enableSwap;
        using var id = ImRaii.PushId(@$"{biteName}");

        DrawUtil.DrawCheckboxTree(biteName, ref HooksetEnabled,
            () =>
            {
                ConditionSet = Ui.ConditionUi.DrawConditionSetSlim(
                    UIStrings.Conditions,
                    ConditionSet,
                    Ui.ConditionScope.Hook,
                    showAdvanced: true,
                    showSubPrefix: false);

                if (EnableHooksetSwap)
                    DrawUtil.DrawTreeNodeEx(UIStrings.HookType, DrawBite, UIStrings.HookWillBeUsedIfPatienceIsNotUp);
            });
    }

    private void DrawBite()
    {
        using var indent = ImRaii.PushIndent();

        DrawUtil.Checkbox(UIStrings.UseMutlipleHooksByTimer, ref UseMultipleHookTypesByTimer);

        if (!UseMultipleHookTypesByTimer)
        {
            if (ImGui.RadioButton(UIStrings.Normal_Hook, HooksetType == HookType.Normal))
            {
                HooksetType = HookType.Normal;
                Service.Save();
            }

            if (ImGui.RadioButton(UIStrings.PrecisionHookset, HooksetType == HookType.Precision))
            {
                HooksetType = HookType.Precision;
                Service.Save();
            }

            if (ImGui.RadioButton(UIStrings.PowerfulHookset, HooksetType == HookType.Powerful))
            {
                HooksetType = HookType.Powerful;
                Service.Save();
            }

            if (ImGui.RadioButton(UIStrings.StellarHookset, HooksetType == HookType.Stellar))
            {
                HooksetType = HookType.Stellar;
                Service.Save();
            }
        }
        else
        {
            DrawTimedHookTypeOption(UIStrings.Normal_Hook, HookType.Normal,
                ref UseNormalHookTypeByTimer, ref NormalHookTypeMin, ref NormalHookTypeMax);

            DrawTimedHookTypeOption(UIStrings.PrecisionHookset, HookType.Precision,
                ref UsePrecisionHookTypeByTimer, ref PrecisionHookTypeMin, ref PrecisionHookTypeMax);

            DrawTimedHookTypeOption(UIStrings.PowerfulHookset, HookType.Powerful,
                ref UsePowerfulHookTypeByTimer, ref PowerfulHookTypeMin, ref PowerfulHookTypeMax);

            DrawTimedHookTypeOption(UIStrings.StellarHookset, HookType.Stellar,
                ref UseStellarHookTypeByTimer, ref StellarHookTypeMin, ref StellarHookTypeMax);
        }
    }

    private void DrawTimedHookTypeOption(string label, HookType hookType, ref bool enabled, ref double minTime, ref double maxTime)
    {
        using var id = ImRaii.PushId(label);
        using var indent = ImRaii.PushIndent();

        if (DrawUtil.Checkbox(label, ref enabled))
        {
            if (enabled && HooksetType == HookType.None)
                HooksetType = hookType;
        }

        if (enabled)
        {
            using var innerIndent = ImRaii.PushIndent();
            ImGui.TextColored(ImGuiColors.DalamudYellow, UIStrings.SetZeroToIgnore);
            SetupTimer(ref minTime, ref maxTime);
        }
    }

    private void SetupTimer(ref double minTimeDelay, ref double maxTimeDelay)
    {

        ImGui.SetNextItemWidth(100 * ImGuiHelpers.GlobalScale);
        if (ImGui.InputDouble(UIStrings.MinWait, ref minTimeDelay, .1, 1, @"%.1f%"))
        {
            switch (minTimeDelay)
            {
                case <= 0:
                    minTimeDelay = 0;
                    break;
                case > 99:
                    minTimeDelay = 99;
                    break;
            }

            Service.Save();
        }

        ImGui.SameLine();
        ImGuiComponents.HelpMarker($"{UIStrings.HelpMarkerMinWaitTimer}\n\n{UIStrings.DoesntHaveAffectUnderChum}");

        ImGui.SetNextItemWidth(100 * ImGuiHelpers.GlobalScale);
        if (ImGui.InputDouble(UIStrings.MaxWait, ref maxTimeDelay, .1, 1, @"%.1f%"))
        {
            switch (maxTimeDelay)
            {
                case 0.1:
                    maxTimeDelay = 2;
                    break;
                case <= 0:
                case <= 1.9: //This makes the option turn off if delay = 2 seconds when clicking the minus.
                    maxTimeDelay = 0;
                    break;
                case > 99:
                    maxTimeDelay = 99;
                    break;
            }

            Service.Save();
        }

        ImGuiComponents.HelpMarker(UIStrings.HelpMarkerMaxWaitTimer);
    }
}
