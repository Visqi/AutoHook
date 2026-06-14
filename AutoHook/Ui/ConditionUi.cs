using AutoHook.Conditions;
using AutoHook.Conditions.Definitions;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility.Raii;
using Newtonsoft.Json;
using static AutoHook.Conditions.ConditionRegistry;
using static AutoHook.Conditions.IConditionDefinition;

namespace AutoHook.Ui;

public enum ConditionScope {
    Hook,
    AutoCordial,
    FishIgnore,
    AutoCast,
}

public static class ConditionUi {
    private static ConditionSet? _clipboard;
    private static string _exportBase64 = string.Empty;
    private static int _forceOpenConditionUiId;

    public static bool IsConditionCurrentlyTrue(Condition cond)
        => cond.Enabled && cond.Evaluate(Service.WorldState, Registry);

    public static bool IsGroupCurrentlyTrue(ConditionGroup group)
        => group.Enabled
           && group.Conditions.Any(c => c.Enabled)
           && group.Evaluate(Service.WorldState, Registry);

    public static ConditionSet? DrawConditionSet(string label, ConditionSet? set, ConditionScope scope, bool showPresets = true) {
        using var tree = ImRaii.TreeNode(label, ImGuiTreeNodeFlags.FramePadding | ImGuiTreeNodeFlags.SpanAvailWidth);
        if (!tree)
            return set;

        set ??= new ConditionSet();

        using var id = ImRaii.PushId(label);
        {
            DrawSetHeader(set);
            ImGui.Spacing();

            for (var gi = 0; gi < set.Groups.Count; gi++) {
                var group = set.Groups[gi];
                var groupLetter = (char)('A' + gi);
                using var _ = ImRaii.PushId($"grp{gi}");

                var deleteGroup = false;
                using (IsGroupCurrentlyTrue(group) ? ImRaii.PushColor(ImGuiCol.Text, ImGuiColors.ParsedGreen) : null)
                    if (ImGui.CollapsingHeader($"Group {groupLetter}###grp_header", ImGuiTreeNodeFlags.DefaultOpen)) {
                        deleteGroup = DrawGroupHeader(set, group, gi, scope);
                        ImGui.Spacing();

                        // Presets apply to this specific group
                        if (showPresets) {
                            ConditionPresetsUi.DrawScopePresets(scope, set, group);
                            ImGui.Spacing();
                        }

                        DrawConditions(group, scope);
                    }

                if (deleteGroup) {
                    set.Groups.RemoveAt(gi);
                    gi--;
                    continue;
                }
            }
        }

        return set;
    }

    private static bool RequiresComplexConditionUi(ConditionSet set) => set.Groups.Count > 1;

    public static ConditionSet? DrawConditionSetSlim(string label, ConditionSet? set, ConditionScope scope, bool showAdvanced = true, IReadOnlyList<string>? allowedTypeIds = null, bool showSubPrefix = false, Action? drawHeaderExtras = null) {
        set ??= new ConditionSet();
        if (set.Groups.Count == 0)
            set.Groups.Add(new ConditionGroup());

        // Either slim view or advanced view, never both. Toggle via SlimAdvancedExpanded.
        // Multi-group sets always use the advanced editor so every group stays visible.
        if (set.SlimAdvancedExpanded || RequiresComplexConditionUi(set)) {
            DrawSlimAdvancedEditor(set, scope, drawHeaderExtras);
            return set;
        }

        var group = set.Groups[0];
        var types = allowedTypeIds != null && allowedTypeIds.Count > 0
            ? GetScopedTypes(scope).Where(d => allowedTypeIds.Contains(d.Id)).ToList()
            : [.. GetScopedTypes(scope)];
        var defaultTypeId = types.FirstOrDefault()?.Id ?? Registry.GetId<StatusActiveCD>();

        // Header: (optional) sub marker + label + add + small advanced icon + optional extras.
        if (!label.IsNullOrEmpty()) {
            if (showSubPrefix) {
                ImGui.TextUnformatted(" └");
                ImGui.SameLine();
            }

            ImGui.TextUnformatted(label);
            ImGui.SameLine();
        }

        var newlyAddedIndex = -1;
        if (ImGuiComponents.IconButton(FontAwesomeIcon.Plus)) {
            newlyAddedIndex = group.Conditions.Count;
            group.Conditions.Add(new Condition { TypeId = defaultTypeId, Params = [] });
            Service.Save();
        }
        ImGui.TooltipOnHover("Add condition");

        ImGui.SameLine(0, 3.Scaled());
        if (showAdvanced && ImGuiComponents.IconButton(FontAwesomeIcon.Code)) {
            set.SlimAdvancedExpanded = true;
            Service.Save();
        }
        ImGui.TooltipOnHover("Advanced (groups, expression)");

        if (drawHeaderExtras != null) {
            ImGui.SameLine(0, 3.Scaled());
            drawHeaderExtras();
        }

        var toRemove = new List<int>();
        for (var ci = 0; ci < group.Conditions.Count; ci++) {
            var cond = group.Conditions[ci];
            cond.EnsureUiId();
            using var _ = ImRaii.PushId($"slim_cond{cond.UiId}");
            var rowLabel = types.FirstOrDefault(d => d.Id == cond.TypeId)?.Name ?? cond.TypeId;
            var enabled = cond.Enabled;

            var forceOpen = ci == newlyAddedIndex || cond.UiId == _forceOpenConditionUiId;

            DrawUtil.DrawCheckboxTree(rowLabel, ref enabled, () => {
                if (DrawConditionContent(cond, scope, types)) {
                    _forceOpenConditionUiId = cond.UiId;
                    Service.Save();
                }
                ImGui.SameLine();
                if (ImGuiComponents.IconButton(FontAwesomeIcon.Trash))
                    toRemove.Add(ci);
                ImGui.TooltipOnHover("Delete condition");
            }, forceOpen: forceOpen, highlightLabel: IsConditionCurrentlyTrue(cond));

            if (cond.UiId == _forceOpenConditionUiId && forceOpen)
                _forceOpenConditionUiId = 0;
            if (enabled != cond.Enabled)
                cond.Enabled = enabled;
        }
        foreach (var idx in toRemove.OrderByDescending(x => x)) {
            group.Conditions.RemoveAt(idx);
        }
        if (toRemove.Count > 0)
            Service.Save();

        return set;
    }

    private static void DrawSlimAdvancedEditor(ConditionSet set, ConditionScope scope, Action? drawHeaderExtras) {
        // Back icon inline with advanced editor controls (set header).
        if (!RequiresComplexConditionUi(set)) {
            if (ImGuiComponents.IconButton(FontAwesomeIcon.ArrowLeft)) {
                set.SlimAdvancedExpanded = false;
                Service.Save();
            }
            ImGui.TooltipOnHover("Back to simple view");
            ImGui.SameLine();
        }
        DrawSetHeader(set);
        if (drawHeaderExtras != null) {
            ImGui.SameLine(0, 3.Scaled());
            drawHeaderExtras();
        }
        ImGui.Spacing();
        var toRemoveGroup = new List<int>();
        for (var gi = 0; gi < set.Groups.Count; gi++) {
            var g = set.Groups[gi];
            var groupLetter = (char)('A' + gi);
            using var _ = ImRaii.PushId($"slim_adv_grp{gi}");
            var gEnabled = g.Enabled;
            DrawUtil.DrawCheckboxTree($"Group {groupLetter}", ref gEnabled, () => {
                var deleteGroup = DrawGroupHeader(set, g, gi, scope);
                if (deleteGroup)
                    toRemoveGroup.Add(gi);
                ImGui.Spacing();
                DrawConditionsWithTypes(g, scope, [.. GetScopedTypes(scope)]);
            }, highlightLabel: IsGroupCurrentlyTrue(g));
            if (gEnabled != g.Enabled)
                g.Enabled = gEnabled;
        }
        foreach (var idx in toRemoveGroup.OrderByDescending(x => x)) {
            set.Groups.RemoveAt(idx);
        }
        if (toRemoveGroup.Count > 0)
            Service.Save();
    }

    private static void DrawSetHeader(ConditionSet set) {
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

        ImGui.SameLine();
        if (ImGuiComponents.IconButton(FontAwesomeIcon.ShareAlt)) {
            ImGui.OpenPopup("CondExport");
        }
        ImGui.TooltipOnHover("Export/import conditions & expression (Base64)");

        using (var popup = ImRaii.Popup("CondExport")) {
            if (popup.Success) {
                ImGui.TextColored(ImGuiColors.DalamudYellow, "Conditions / Expression Export");
                ImGui.Separator();

                if (ImGui.Button("Export conditions")) {
                    var json = JsonConvert.SerializeObject(set);
                    var bytes = Encoding.UTF8.GetBytes(json);
                    _exportBase64 = Convert.ToBase64String(bytes);
                    ImGui.SetClipboardText(_exportBase64);
                }

                ImGui.SameLine();
                if (ImGui.Button("Export expression")) {
                    var expr = set.Expression ?? string.Empty;
                    var bytes = Encoding.UTF8.GetBytes(expr);
                    var exprBase64 = Convert.ToBase64String(bytes);
                    ImGui.SetClipboardText(exprBase64);
                }

                if (ImGui.Button("Import conditions")) {
                    try {
                        var fromClipboard = ImGui.GetClipboardText();
                        var data = Convert.FromBase64String(fromClipboard.Trim());
                        var json = Encoding.UTF8.GetString(data);
                        var imported = JsonConvert.DeserializeObject<ConditionSet>(json);
                        if (imported != null && imported.Groups.Count > 0) {
                            ApplyClipboard(set, imported);
                            Service.Save();
                        }
                    }
                    catch { }
                }

                ImGui.SameLine();
                if (ImGui.Button("Import expression")) {
                    try {
                        var fromClipboard = ImGui.GetClipboardText();
                        var data = Convert.FromBase64String(fromClipboard.Trim());
                        var expr = Encoding.UTF8.GetString(data);
                        set.Expression = string.IsNullOrWhiteSpace(expr) ? null : expr;
                        Service.Save();
                    }
                    catch { }
                }
            }
        }

        if (!set.ExprVisible)
            return;

        ConditionExpressionUi.DrawExpressionEditor(set);
    }

    private static bool DrawGroupHeader(ConditionSet set, ConditionGroup group, int index, ConditionScope scope) {
        var mode = group.CombineMode;
        var btn = mode == ConditionCombineMode.All ? "&&" : "||";
        if (ImGui.Button(btn))
            group.CombineMode = mode == ConditionCombineMode.All ? ConditionCombineMode.Any : ConditionCombineMode.All;
        ImGui.TooltipOnHover($"Evaluate: {(mode is ConditionCombineMode.All ? "AND" : "OR")} - {(mode is ConditionCombineMode.All ? "all" : "any")} must be true");

        ImGui.SameLine();
        if (ImGuiComponents.IconButton(FontAwesomeIcon.PlusCircle)) {
            group.Conditions.Add(new Condition {
                TypeId = GetScopedTypes(scope).FirstOrDefault()?.Id ?? Registry.GetId<StatusActiveCD>(),
                Params = []
            });
        }

        if (set.Groups.Count > 1 || group.Conditions.Count > 0) {
            ImGui.SameLine();
            if (ImGuiComponents.IconButton(FontAwesomeIcon.Trash)) {
                if (ImGui.GetIO().KeyShift)
                    return true;
                group.Conditions.Clear();
            }
            ImGui.TooltipOnHover("Click: clear conditions in this group\nShift+Click: delete group");
        }

        return false;
    }

    private static void DrawConditions(ConditionGroup group, ConditionScope scope)
        => DrawConditionsWithTypes(group, scope, [.. GetScopedTypes(scope)]);

    private static bool DrawConditionContent(Condition cond, ConditionScope scope, List<ConditionTypeDef> defs) {
        var typeChanged = false;
        DrawInverseToggle(cond);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(180.Scaled());
        var currentDef = defs.FirstOrDefault(d => d.Id == cond.TypeId) ?? defs.FirstOrDefault();
        var currentLabel = currentDef?.Name ?? cond.TypeId;
        using (var combo = ImRaii.Combo("##type", currentLabel)) {
            if (combo) {
                foreach (var def in defs) {
                    var sel = def.Id == cond.TypeId;
                    if (ImGui.Selectable($"{def.Name}##{def.Id}", sel)) {
                        cond.TypeId = def.Id;
                        cond.Params.Clear();
                        typeChanged = true;
                    }
                }
            }
        }
        ImGui.SameLine();
        ConditionParamUi.DrawParams(cond);
        return typeChanged;
    }

    private static void DrawConditionsWithTypes(ConditionGroup group, ConditionScope scope, List<ConditionTypeDef> defs) {
        for (var ci = 0; ci < group.Conditions.Count; ci++) {
            var cond = group.Conditions[ci];
            cond.EnsureUiId();
            using var _ = ImRaii.PushId($"cond{cond.UiId}");
            using (IsConditionCurrentlyTrue(cond) ? ImRaii.PushColor(ImGuiCol.Text, ImGuiColors.ParsedGreen) : null)
                if (DrawConditionContent(cond, scope, defs))
                    Service.Save();
            ImGui.SameLine();
            if (ImGuiComponents.IconButton(FontAwesomeIcon.Trash)) {
                group.Conditions.RemoveAt(ci);
                ci--;
                continue;
            }
        }
    }

    private static IEnumerable<ConditionTypeDef> GetScopedTypes(ConditionScope scope) {
        var all = Registry.All;
        var flag = scope switch {
            ConditionScope.Hook => ConditionScopeFlags.Hook,
            ConditionScope.AutoCordial => ConditionScopeFlags.AutoCordial,
            ConditionScope.FishIgnore => ConditionScopeFlags.FishIgnore,
            ConditionScope.AutoCast => ConditionScopeFlags.AutoCast,
            _ => ConditionScopeFlags.All,
        };
        return all.Where(d => (d.AllowedScopes & flag) != 0);
    }

    private static void DrawInverseToggle(Condition cond) {
        var inv = GetBool(cond.Params, "inv", false);
        var color = inv ? ImGuiColors.DalamudOrange : ImGuiColors.DalamudGrey;
        using (ImRaii.PushColor(ImGuiCol.Text, color)) {
            if (ImGuiComponents.IconButton(FontAwesomeIcon.Exclamation)) {
                inv = !inv;
                if (inv)
                    cond.Params["inv"] = true;
                else
                    cond.Params.Remove("inv");
            }
        }
        ImGui.TooltipOnHover("Evaluate: NOT - condition must be false");
    }

    private static ConditionSet CloneConditionSet(ConditionSet src) {
        var clone = new ConditionSet {
            CombineMode = src.CombineMode,
            Groups = []
        };

        foreach (var g in src.Groups) {
            var cg = new ConditionGroup {
                CombineMode = g.CombineMode,
                Conditions = [],
                Enabled = g.Enabled
            };

            foreach (var c in g.Conditions) {
                var nc = new Condition {
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

    private static Dictionary<string, object> CloneParams(Dictionary<string, object> src) {
        var dict = new Dictionary<string, object>(src.Count);
        foreach (var (key, value) in src) {
            dict[key] = value is List<object> list ? new List<object>(list) : value;
        }
        return dict;
    }

    private static void ApplyClipboard(ConditionSet target, ConditionSet source) {
        target.CombineMode = source.CombineMode;
        target.Expression = source.Expression;
        target.Groups.Clear();
        foreach (var g in source.Groups) {
            var cg = new ConditionGroup {
                CombineMode = g.CombineMode,
                Conditions = [],
                Enabled = g.Enabled
            };

            foreach (var c in g.Conditions) {
                var nc = new Condition {
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

