using AutoHook.Conditions;
using AutoHook.Conditions.Definitions;
using AutoHook.Utils;
using static AutoHook.Conditions.ConditionRegistry;
using static AutoHook.Conditions.IConditionDefinition;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace AutoHook.Ui;

public enum ConditionScope
{
    Hook,
    AutoCordial,
    FishIgnore,
    AutoCast,
}

public static class ConditionUi
{
    private static ConditionSet? _clipboard;

    public static ConditionSet? DrawConditionSet(string label, ConditionSet? set, ConditionScope scope, bool showPresets = true)
    {
        using var tree = ImRaii.TreeNode(label, ImGuiTreeNodeFlags.FramePadding | ImGuiTreeNodeFlags.SpanAvailWidth);
        if (!tree)
            return set;

        set ??= new ConditionSet();

        using var id = ImRaii.PushId(label);
        {
            DrawSetHeader(set);
            ImGui.Spacing();

            for (var gi = 0; gi < set.Groups.Count; gi++)
            {
                var group = set.Groups[gi];
                var groupLetter = (char)('A' + gi);
                using var _ = ImRaii.PushId($"grp{gi}");

                var deleteGroup = false;
                if (ImGui.CollapsingHeader($"Group {groupLetter}###grp_header", ImGuiTreeNodeFlags.DefaultOpen))
                {
                    deleteGroup = DrawGroupHeader(set, group, gi, scope);
                    ImGui.Spacing();

                    // Presets apply to this specific group
                    if (showPresets)
                    {
                        ConditionPresetsUi.DrawScopePresets(scope, set, group);
                        ImGui.Spacing();
                    }

                    DrawConditions(group, scope);
                }

                ImGui.DragDropSource(gi, "COND_GROUP"u8, $"Moving group {groupLetter}");
                ImGui.DragDropTarget(gi, "COND_GROUP"u8, set.Groups.Count, (sourceIndex, insertIndex) =>
                {
                    if (sourceIndex == insertIndex || sourceIndex < 0 || sourceIndex >= set.Groups.Count)
                        return;

                    var g = set.Groups[sourceIndex];
                    set.Groups.RemoveAt(sourceIndex);

                    // after removal, clamp insert index
                    insertIndex = Math.Clamp(insertIndex, 0, set.Groups.Count);
                    set.Groups.Insert(insertIndex, g);
                });

                if (deleteGroup)
                {
                    set.Groups.RemoveAt(gi);
                    gi--;
                    continue;
                }
            }
        }

        return set;
    }

    public static ConditionSet? DrawConditionSetSlim(string label, ConditionSet? set, ConditionScope scope, bool showAdvanced = true, IReadOnlyList<string>? allowedTypeIds = null, bool showSubPrefix = false, Action? drawHeaderExtras = null)
    {
        set ??= new ConditionSet();
        if (set.Groups.Count == 0)
            set.Groups.Add(new ConditionGroup());

        // Either slim view or advanced view, never both. Toggle via SlimAdvancedExpanded.
        if (set.SlimAdvancedExpanded)
        {
            DrawSlimAdvancedEditor(set, scope, drawHeaderExtras);
            return set;
        }

        var group = set.Groups[0];
        var types = allowedTypeIds != null && allowedTypeIds.Count > 0
            ? GetScopedTypes(scope).Where(d => allowedTypeIds.Contains(d.Id)).ToList()
            : GetScopedTypes(scope).ToList();
        var defaultTypeId = types.FirstOrDefault()?.Id ?? Registry.GetId<StatusActiveCD>();

        // Header: (optional) sub marker + label + add + small advanced icon + optional extras.
        if (!label.IsNullOrEmpty())
        {
            if (showSubPrefix)
            {
                ImGui.TextUnformatted(" └");
                ImGui.SameLine();
            }

            ImGui.TextUnformatted(label);
            ImGui.SameLine();
        }

        var newlyAddedIndex = -1;
        if (ImGuiComponents.IconButton(FontAwesomeIcon.Plus))
        {
            newlyAddedIndex = group.Conditions.Count;
            group.Conditions.Add(new Condition { TypeId = defaultTypeId, Params = [] });
            Service.Save();
        }
        ImGui.TooltipOnHover("Add condition");

        ImGui.SameLine(0, 3);
        if (showAdvanced && ImGuiComponents.IconButton(FontAwesomeIcon.Code))
        {
            set.SlimAdvancedExpanded = true;
            Service.Save();
        }
        ImGui.TooltipOnHover("Advanced (groups, expression)");

        if (drawHeaderExtras != null)
        {
            ImGui.SameLine(0, 3);
            drawHeaderExtras();
        }

        var toRemove = new List<int>();
        for (var ci = 0; ci < group.Conditions.Count; ci++)
        {
            var cond = group.Conditions[ci];
            cond.EnsureUiId();
            using var _ = ImRaii.PushId($"slim_cond{cond.UiId}");
            var rowLabel = types.FirstOrDefault(d => d.Id == cond.TypeId)?.Name ?? cond.TypeId;
            var enabled = cond.Enabled;

            var forceOpen = ci == newlyAddedIndex;

            DrawUtil.DrawCheckboxTree(rowLabel, ref enabled, () =>
            {
                DrawConditionContent(cond, scope, types);
                ImGui.SameLine();
                if (ImGuiComponents.IconButton(FontAwesomeIcon.Trash))
                    toRemove.Add(ci);
                ImGui.TooltipOnHover("Delete condition");
            }, forceOpen: forceOpen);
            if (enabled != cond.Enabled)
            {
                cond.Enabled = enabled;
                Service.Save();
            }
        }
        foreach (var idx in toRemove.OrderByDescending(x => x))
        {
            group.Conditions.RemoveAt(idx);
        }
        if (toRemove.Count > 0)
            Service.Save();

        return set;
    }

    private static void DrawSlimAdvancedEditor(ConditionSet set, ConditionScope scope, Action? drawHeaderExtras)
    {
        // Back icon inline with advanced editor controls (set header).
        if (ImGuiComponents.IconButton(FontAwesomeIcon.ArrowLeft))
        {
            set.SlimAdvancedExpanded = false;
            Service.Save();
        }
        ImGui.TooltipOnHover("Back to simple view");
        ImGui.SameLine();
        DrawSetHeader(set);
        if (drawHeaderExtras != null)
        {
            ImGui.SameLine(0, 3);
            drawHeaderExtras();
        }
        ImGui.Spacing();
        var toRemoveGroup = new List<int>();
        for (var gi = 0; gi < set.Groups.Count; gi++)
        {
            var g = set.Groups[gi];
            var groupLetter = (char)('A' + gi);
            using var _ = ImRaii.PushId($"slim_adv_grp{gi}");
            var gEnabled = g.Enabled;
            DrawUtil.DrawCheckboxTree($"Group {groupLetter}", ref gEnabled, () =>
            {
                var deleteGroup = DrawGroupHeader(set, g, gi, scope);
                if (deleteGroup)
                    toRemoveGroup.Add(gi);
                ImGui.Spacing();
                DrawConditionsWithTypes(g, scope, GetScopedTypes(scope).ToList());
            });
            if (gEnabled != g.Enabled)
            {
                g.Enabled = gEnabled;
                Service.Save();
            }
            ImGui.DragDropSource(gi, "COND_GROUP"u8, $"Moving group {groupLetter}");
            ImGui.DragDropTarget(gi, "COND_GROUP"u8, set.Groups.Count, (sourceIndex, insertIndex) =>
            {
                if (sourceIndex == insertIndex || sourceIndex < 0 || sourceIndex >= set.Groups.Count) return;
                var grp = set.Groups[sourceIndex];
                set.Groups.RemoveAt(sourceIndex);
                insertIndex = Math.Clamp(insertIndex, 0, set.Groups.Count);
                set.Groups.Insert(insertIndex, grp);
            });
        }
        foreach (var idx in toRemoveGroup.OrderByDescending(x => x))
        {
            set.Groups.RemoveAt(idx);
        }
        if (toRemoveGroup.Count > 0)
            Service.Save();
    }

    private static void DrawSetHeader(ConditionSet set)
    {
        var mode = set.CombineMode;
        var btn = mode == ConditionCombineMode.All ? "&&" : "||";

        if (ImGuiComponents.IconButton(FontAwesomeIcon.Plus))
            set.Groups.Add(new ConditionGroup());
        ImGui.TooltipOnHover("Add group");

        ImGui.SameLine();
        if (ImGui.Button(btn))
            set.CombineMode = mode == ConditionCombineMode.All ? ConditionCombineMode.Any : ConditionCombineMode.All;
        ImGui.TooltipOnHover($"Evaluate: {(mode is ConditionCombineMode.All ? "AND" : "OR")} - {(mode is ConditionCombineMode.All ? "all" : "any")} must be true");

        ImGui.SameLine();
        if (ImGuiComponents.IconButton(FontAwesomeIcon.Code))
            set.ExprVisible = !set.ExprVisible;
        ImGui.TooltipOnHover(set.ExprVisible ? "Hide editor" : "Show editor");

        ImGui.SameLine();
        if (ImGuiComponents.IconButton(FontAwesomeIcon.Copy))
            _clipboard = CloneConditionSet(set);

        ImGui.SameLine();
        if (ImGuiComponents.IconButton(FontAwesomeIcon.Clipboard))
            if (_clipboard != null && _clipboard.Groups.Count > 0)
                ApplyClipboard(set, _clipboard);

        if (!set.ExprVisible)
            return;

        ConditionExpressionUi.DrawExpressionEditor(set);
    }

    private static bool DrawGroupHeader(ConditionSet set, ConditionGroup group, int index, ConditionScope scope)
    {
        var mode = group.CombineMode;
        var btn = mode == ConditionCombineMode.All ? "&&" : "||";
        if (ImGui.Button(btn))
            group.CombineMode = mode == ConditionCombineMode.All ? ConditionCombineMode.Any : ConditionCombineMode.All;
        ImGui.TooltipOnHover($"Evaluate: {(mode is ConditionCombineMode.All ? "AND" : "OR")} - {(mode is ConditionCombineMode.All ? "all" : "any")} must be true");

        ImGui.SameLine();
        if (ImGuiComponents.IconButton(FontAwesomeIcon.PlusCircle))
        {
            group.Conditions.Add(new Condition
            {
                TypeId = GetScopedTypes(scope).FirstOrDefault()?.Id ?? Registry.GetId<StatusActiveCD>(),
                Params = []
            });
        }

        ImGui.SameLine();
        if (ImGuiComponents.IconButton(FontAwesomeIcon.Trash))
        {
            if (ImGui.GetIO().KeyShift)
                return true;
            group.Conditions.Clear();
        }
        ImGui.TooltipOnHover("Click: clear conditions in this group\nShift+Click: delete group");

        return false;
    }

    private static void DrawConditions(ConditionGroup group, ConditionScope scope)
        => DrawConditionsWithTypes(group, scope, GetScopedTypes(scope).ToList());

    private static void DrawConditionContent(Condition cond, ConditionScope scope, List<ConditionTypeDef> defs)
    {
        DrawInverseToggle(cond);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(180 * ImGuiHelpers.GlobalScale);
        var currentDef = defs.FirstOrDefault(d => d.Id == cond.TypeId) ?? defs.FirstOrDefault();
        var currentLabel = currentDef?.Name ?? cond.TypeId;
        using (var combo = ImRaii.Combo("##type", currentLabel))
        {
            if (combo)
            {
                foreach (var def in defs)
                {
                    var sel = def.Id == cond.TypeId;
                    if (ImGui.Selectable(def.Name, sel))
                    {
                        cond.TypeId = def.Id;
                        cond.Params.Clear();
                    }
                }
            }
        }
        ImGui.SameLine();
        ConditionParamUi.DrawParams(cond);
    }

    private static void DrawConditionsWithTypes(ConditionGroup group, ConditionScope scope, List<ConditionTypeDef> defs)
    {
        for (var ci = 0; ci < group.Conditions.Count; ci++)
        {
            var cond = group.Conditions[ci];
            cond.EnsureUiId();
            using var _ = ImRaii.PushId($"cond{cond.UiId}");
            DrawConditionContent(cond, scope, defs);
            ImGui.SameLine();
            if (ImGuiComponents.IconButton(FontAwesomeIcon.Trash))
            {
                group.Conditions.RemoveAt(ci);
                ci--;
                continue;
            }
        }
    }

    private static IEnumerable<ConditionTypeDef> GetScopedTypes(ConditionScope scope)
    {
        var all = ConditionRegistry.Registry.All;
        var flag = scope switch
        {
            ConditionScope.Hook => ConditionScopeFlags.Hook,
            ConditionScope.AutoCordial => ConditionScopeFlags.AutoCordial,
            ConditionScope.FishIgnore => ConditionScopeFlags.FishIgnore,
            ConditionScope.AutoCast => ConditionScopeFlags.AutoCast,
            _ => ConditionScopeFlags.All,
        };
        return all.Where(d => (d.AllowedScopes & flag) != 0);
    }

    private static void DrawInverseToggle(Condition cond)
    {
        var inv = GetBool(cond.Params, "inv", false);
        var color = inv ? ImGuiColors.DalamudOrange : ImGuiColors.DalamudGrey;
        using (ImRaii.PushColor(ImGuiCol.Text, color))
        {
            if (ImGuiComponents.IconButton(FontAwesomeIcon.Exclamation))
            {
                inv = !inv;
                if (inv)
                    cond.Params["inv"] = true;
                else
                    cond.Params.Remove("inv");
            }
        }
        ImGui.TooltipOnHover("Evaluate: NOT - condition must be false");
    }

    private static ConditionSet CloneConditionSet(ConditionSet src)
    {
        var clone = new ConditionSet
        {
            CombineMode = src.CombineMode,
            Groups = []
        };

        foreach (var g in src.Groups)
        {
            var cg = new ConditionGroup
            {
                CombineMode = g.CombineMode,
                Conditions = [],
                Enabled = g.Enabled
            };

            foreach (var c in g.Conditions)
            {
                var nc = new Condition
                {
                    TypeId = c.TypeId,
                    Params = CloneParams(c.Params),
                    Enabled = c.Enabled
                };
                cg.Conditions.Add(nc);
            }

            clone.Groups.Add(cg);
        }

        return clone;
    }

    private static Dictionary<string, object> CloneParams(IReadOnlyDictionary<string, object> src)
    {
        var dict = new Dictionary<string, object>(src.Count);
        foreach (var (key, value) in src)
        {
            dict[key] = value is List<object> list ? new List<object>(list) : value;
        }
        return dict;
    }

    private static void ApplyClipboard(ConditionSet target, ConditionSet source)
    {
        target.CombineMode = source.CombineMode;
        target.Expression = source.Expression;
        target.Groups.Clear();
        foreach (var g in source.Groups)
        {
            var cg = new ConditionGroup
            {
                CombineMode = g.CombineMode,
                Conditions = [],
                Enabled = g.Enabled
            };

            foreach (var c in g.Conditions)
            {
                var nc = new Condition
                {
                    TypeId = c.TypeId,
                    Params = CloneParams(c.Params),
                    Enabled = c.Enabled
                };
                cg.Conditions.Add(nc);
            }

            target.Groups.Add(cg);
        }
    }
}

