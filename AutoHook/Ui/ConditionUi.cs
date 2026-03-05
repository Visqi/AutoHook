using AutoHook.Conditions;
using AutoHook.Conditions.Definitions;
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
        {
            set.ExprVisible = !set.ExprVisible;
        }
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
    {
        var defs = GetScopedTypes(scope).ToList();

        for (var ci = 0; ci < group.Conditions.Count; ci++)
        {
            var cond = group.Conditions[ci];
            using var _ = ImRaii.PushId($"cond{ci}");

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
                Conditions = []
            };

            foreach (var c in g.Conditions)
            {
                var nc = new Condition
                {
                    TypeId = c.TypeId,
                    Params = CloneParams(c.Params)
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
                Conditions = []
            };

            foreach (var c in g.Conditions)
            {
                var nc = new Condition
                {
                    TypeId = c.TypeId,
                    Params = CloneParams(c.Params)
                };
                cg.Conditions.Add(nc);
            }

            target.Groups.Add(cg);
        }
    }
}

