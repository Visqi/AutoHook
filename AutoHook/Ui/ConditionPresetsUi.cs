using AutoHook.Conditions;
using AutoHook.Conditions.Definitions;
using static AutoHook.Conditions.ConditionRegistry;
using static AutoHook.Conditions.IConditionDefinition;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace AutoHook.Ui;

public static class ConditionPresetsUi
{
    public static void DrawScopePresets(ConditionScope scope, ConditionSet set, ConditionGroup group)
    {
        switch (scope)
        {
            case ConditionScope.Hook:
                DrawHookPresets(set, group);
                break;
            case ConditionScope.AutoCordial:
                DrawCordialPresets(set, group);
                break;
            case ConditionScope.FishIgnore:
                DrawFishPresets(set, group);
                break;
            case ConditionScope.AutoCast:
                DrawAutoCastPresets(set, group);
                break;
        }
    }

    private static void DrawHookPresets(ConditionSet set, ConditionGroup group)
    {
        ImGui.TextColored(ImGuiColors.DalamudGrey, UIStrings.Presets_);

        if (DrawActionIconButton(IDs.Actions.SurfaceSlap, UIStrings.UseSlapActive))
            AddStatusPreset(set, group, IDs.Status.SurfaceSlap, ImGui.GetIO().KeyShift);

        ImGui.SameLine();

        if (DrawActionIconButton(IDs.Actions.IdenticalCast, UIStrings.UseIcActive))
            AddStatusPreset(set, group, IDs.Status.IdenticalCast, ImGui.GetIO().KeyShift);

        ImGui.SameLine();

        if (DrawActionIconButton(IDs.Actions.PrizeCatch, UIStrings.Use_Prize_Catch_HelpText))
            AddStatusPreset(set, group, IDs.Status.PrizeCatch, ImGui.GetIO().KeyShift);

        ImGui.SameLine();

        if (DrawActionIconButton(IDs.Actions.MultiHook, UIStrings.OnlyHookWhenActiveMultihook))
            AddMultihookPreset(set, group);
    }

    private static void DrawAutoCastPresets(ConditionSet set, ConditionGroup group)
    {
        ImGui.TextColored(ImGuiColors.DalamudGrey, UIStrings.Presets_);

        if (DrawActionIconButton(IDs.Actions.IdenticalCast, UIStrings.UseIcActive))
            AddStatusPreset(set, group, IDs.Status.IdenticalCast, ImGui.GetIO().KeyShift);

        ImGui.SameLine();

        if (DrawActionIconButton(IDs.Actions.SurfaceSlap, UIStrings.UseSlapActive))
            AddStatusPreset(set, group, IDs.Status.SurfaceSlap, ImGui.GetIO().KeyShift);

        ImGui.SameLine();

        if (DrawStatusIconButton(IDs.Status.FishersIntuition, UIStrings.OnlyUseWhenFisherSIntutionIsActive))
        {
            var inverse = ImGui.GetIO().KeyShift;
            var typeId = Registry.GetId<IntuitionActiveCD>();
            if (!group.Conditions.Any(c => c.TypeId == typeId))
            {
                var cond = new Condition
                {
                    TypeId = typeId,
                    Params = inverse
                        ? new Dictionary<string, object> { ["inv"] = true }
                        : []
                };
                group.Conditions.Add(cond);
            }
            else if (inverse)
            {
                foreach (var c in group.Conditions.Where(c => c.TypeId == typeId))
                    c.Params["inv"] = true;
            }
        }

        ImGui.SameLine();

        if (DrawStatusIconButton(IDs.Status.AnglersFortune, UIStrings.Only_When_Patience_Active))
            AddStatusPreset(set, group, IDs.Status.AnglersFortune, ImGui.GetIO().KeyShift);

        ImGui.SameLine();

        if (DrawStatusIconButton(IDs.Status.MakeshiftBait, UIStrings.OnlyUseWhenMakeshiftBaitActive))
            AddStatusPreset(set, group, IDs.Status.MakeshiftBait, ImGui.GetIO().KeyShift);

        ImGui.SameLine();

        if (DrawActionIconButton(IDs.Actions.Mooch2, UIStrings.AutoCastExtraOptionPatience))
        {
            var typeId = Registry.GetId<ActionAvailableCD>();
            if (!group.Conditions.Any(c => c.TypeId == typeId && c.Params.TryGetValue("id", out var idObj) && Convert.ToUInt32(idObj) == IDs.Actions.Mooch2))
            {
                group.Conditions.Add(new Condition
                {
                    TypeId = typeId,
                    Params = new Dictionary<string, object>
                    {
                        ["id"] = (long)IDs.Actions.Mooch2,
                        ["type"] = 0L,
                        ["inv"] = true
                    }
                });
            }
        }
    }

    private static void DrawCordialPresets(ConditionSet set, ConditionGroup group)
    {
        ImGui.TextColored(ImGuiColors.DalamudGrey, UIStrings.Presets_);

        if (DrawActionIconButton(IDs.Actions.IdenticalCast, UIStrings.Allow_Gp_Overcap))
            AddStatusPreset(set, group, IDs.Status.IdenticalCast, ImGui.GetIO().KeyShift);
    }

    private static void DrawFishPresets(ConditionSet set, ConditionGroup group)
    {
        ImGui.TextColored(ImGuiColors.DalamudGrey, UIStrings.Presets_);

        if (DrawStatusIconButton(IDs.Status.FishersIntuition, "Ignore when Fisher's Intuition is active"))
        {
            var typeId = Registry.GetId<IntuitionActiveCD>();
            if (!group.Conditions.Any(c => c.TypeId == typeId))
            {
                group.Conditions.Add(new Condition
                {
                    TypeId = typeId,
                    Params = []
                });
            }
        }
    }

    private static void AddStatusPreset(ConditionSet set, ConditionGroup group, uint statusId, bool inverse)
    {
        var statusActiveId = Registry.GetId<StatusActiveCD>();
        foreach (var c in group.Conditions.Where(c => c.TypeId == statusActiveId))
        {
            var ids = GetIds(c.Params);
            var inv = GetBool(c.Params, "inv", false);
            if (ids.Contains(statusId) && inv == inverse)
                return; // already present
        }

        var cond = new Condition
        {
            TypeId = statusActiveId,
            Params = new Dictionary<string, object>
            {
                ["ids"] = new List<object> { (long)statusId }
            }
        };
        if (inverse)
            cond.Params["inv"] = true;

        group.Conditions.Add(cond);
    }

    private static void AddMultihookPreset(ConditionSet set, ConditionGroup group)
    {
        var multihookId = Registry.GetId<MultihookAvailableCD>();
        if (group.Conditions.Any(c => c.TypeId == multihookId))
            return;

        group.Conditions.Add(new Condition
        {
            TypeId = multihookId,
            Params = []
        });
    }

    private static bool DrawStatusIconButton(uint statusId, string? tooltip = null)
    {
        var iconId = Lumina.Excel.Sheets.Status.GetRow(statusId).Icon;
        var tex = Svc.Texture.GetFromGameIcon(iconId);
        if (!tex.TryGetWrap(out var wrap, out _))
            return false;

        var size = new System.Numerics.Vector2(24 * ImGuiHelpers.GlobalScale, 24 * ImGuiHelpers.GlobalScale);
        using var color = ImRaii.PushColor(ImGuiCol.Button, 0).Push(ImGuiCol.ButtonHovered, ImGui.GetColorU32(ImGuiCol.ButtonHovered)).Push(ImGuiCol.ButtonActive, ImGui.GetColorU32(ImGuiCol.ButtonActive));
        var clicked = ImGui.ImageButton(wrap.Handle, size);

        ImGui.TooltipOnHover(tooltip ?? MultiString.GetStatusName(statusId));

        return clicked;
    }

    private static bool DrawActionIconButton(uint actionId, string? tooltip = null)
    {
        uint iconId = Lumina.Excel.Sheets.Action.GetRow(actionId).Icon;
        var tex = Svc.Texture.GetFromGameIcon(iconId);
        if (!tex.TryGetWrap(out var wrap, out _))
            return false;

        var size = new System.Numerics.Vector2(24 * ImGuiHelpers.GlobalScale, 24 * ImGuiHelpers.GlobalScale);
        using var color = ImRaii.PushColor(ImGuiCol.Button, 0).Push(ImGuiCol.ButtonHovered, ImGui.GetColorU32(ImGuiCol.ButtonHovered)).Push(ImGuiCol.ButtonActive, ImGui.GetColorU32(ImGuiCol.ButtonActive));
        var clicked = ImGui.ImageButton(wrap.Handle, size);

        ImGui.TooltipOnHover(tooltip ?? MultiString.GetActionName(actionId));

        return clicked;
    }
}

