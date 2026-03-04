using AutoHook.Conditions;
using clib.Extensions;
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
                        DrawScopePresets(scope, set, group);
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

        ImGui.NewLine();
        ImGui.TextColored(ImGuiColors.DalamudGrey, "Expression:");

        var tokens = ParseExpressionTokens(set.Expression, set.Groups.Count);
        var invalidFlags = ValidateExpressionTokens(tokens);
        var selStart = set.ExprSelectionStart;
        var selEnd = set.ExprSelectionEnd;
        var changed = false;
        var moveFrom = -1;
        var moveTo = -1;
        int? deleteIndex = null;

        if (tokens.Count > 0)
        {
            ImGui.SameLine();

            for (var i = 0; i < tokens.Count; i++)
            {
                using var _ = ImRaii.PushId(i);

                var label = GetTokenLabel(tokens[i]);
                var isSelected = selStart.HasValue && selEnd.HasValue && i >= selStart && i <= selEnd;
                var isInvalid = i < invalidFlags.Length && invalidFlags[i];

                using (var colour = ImRaii.PushColor(ImGuiCol.Button, isInvalid ? ImGuiColors.DalamudRed : ImGuiColors.DalamudGrey3, isSelected || isInvalid))
                {
                    if (ImGui.SmallButton(label))
                    {
                        var io = ImGui.GetIO();
                        if (io.KeyShift && selStart.HasValue && selEnd.HasValue)
                        {
                            var start = Math.Min(selStart.Value, i);
                            var end = Math.Max(selEnd.Value, i);
                            set.ExprSelectionStart = start;
                            set.ExprSelectionEnd = end;
                        }
                        else
                        {
                            // untoggling
                            if (selStart.HasValue && selEnd.HasValue && selStart.Value == i && selEnd.Value == i)
                            {
                                set.ExprSelectionStart = null;
                                set.ExprSelectionEnd = null;
                            }
                            else
                            {
                                set.ExprSelectionStart = i;
                                set.ExprSelectionEnd = i;
                            }
                        }
                    }
                }

                ImGui.DragDropSource(i, "COND_EXPR_TOKEN"u8, label);
                ImGui.DragDropTarget(i, "COND_EXPR_TOKEN"u8, tokens.Count, (sourceIndex, insertIndex) =>
                {
                    moveFrom = sourceIndex;
                    moveTo = insertIndex;
                });

                if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
                    deleteIndex = i;

                ImGui.SameLine();
            }

            if (ImGui.SmallButton("x##expr_clear"))
            {
                set.Expression = null;
                set.ExprSelectionStart = null;
                set.ExprSelectionEnd = null;
                tokens.Clear();
                changed = false; // don't rebuild after clear
            }
        }

        // Apply token move
        if (moveFrom >= 0 && moveTo >= 0 && moveFrom < tokens.Count)
        {
            var t = tokens[moveFrom];
            tokens.RemoveAt(moveFrom);
            if (moveTo > moveFrom) moveTo--;
            moveTo = Math.Clamp(moveTo, 0, tokens.Count);
            tokens.Insert(moveTo, t);
            changed = true;
        }

        // Apply delete
        if (deleteIndex.HasValue && deleteIndex.Value >= 0 && deleteIndex.Value < tokens.Count)
        {
            tokens.RemoveAt(deleteIndex.Value);
            changed = true;
        }

        var hasSelection = set.ExprSelectionStart.HasValue && set.ExprSelectionEnd.HasValue
                           && tokens.Count > 0;
        if (hasSelection)
        {
            var start = Math.Min(set.ExprSelectionStart!.Value, set.ExprSelectionEnd!.Value);
            var end = Math.Max(set.ExprSelectionStart.Value, set.ExprSelectionEnd.Value);
            start = Math.Clamp(start, 0, tokens.Count - 1);
            end = Math.Clamp(end, 0, tokens.Count - 1);
            set.ExprSelectionStart = start;
            set.ExprSelectionEnd = end;
        }

        // Palette for adding tokens
        ImGui.TextColored(ImGuiColors.DalamudGrey, "Add:");
        ImGui.SameLine();

        // Group chips A, B, C...
        for (var i = 0; i < set.Groups.Count && i < 26; i++)
        {
            var label = ((char)('A' + i)).ToString();
            if (ImGui.SmallButton(label))
            {
                tokens.Add(new ExprToken(ExprTokenKind.Group, i));
                changed = true;
            }
            ImGui.SameLine();
        }

        if (ImGui.SmallButton("&&##expr_and"))
        {
            tokens.Add(new ExprToken(ExprTokenKind.And));
            changed = true;
        }
        ImGui.SameLine();
        if (ImGui.SmallButton("||##expr_or"))
        {
            tokens.Add(new ExprToken(ExprTokenKind.Or));
            changed = true;
        }
        ImGui.SameLine();
        if (ImGui.SmallButton("("))
        {
            if (hasSelection)
            {
                var start = set.ExprSelectionStart!.Value;
                var end = set.ExprSelectionEnd!.Value;
                tokens.Insert(start, new ExprToken(ExprTokenKind.LParen));
                end++;
                tokens.Insert(end + 1, new ExprToken(ExprTokenKind.RParen));
                set.ExprSelectionStart = start;
                set.ExprSelectionEnd = end;
            }
            else
            {
                tokens.Add(new ExprToken(ExprTokenKind.LParen));
            }
            changed = true;
        }
        ImGui.SameLine();
        if (ImGui.SmallButton(")"))
        {
            if (hasSelection)
            {
                var start = set.ExprSelectionStart!.Value;
                var end = set.ExprSelectionEnd!.Value;
                tokens.Insert(start, new ExprToken(ExprTokenKind.LParen));
                end++;
                tokens.Insert(end + 1, new ExprToken(ExprTokenKind.RParen));
                set.ExprSelectionStart = start;
                set.ExprSelectionEnd = end;
            }
            else
            {
                tokens.Add(new ExprToken(ExprTokenKind.RParen));
            }
            changed = true;
        }

        if (changed)
        {
            if (tokens.Count == 0)
            {
                set.Expression = null;
                set.ExprSelectionStart = null;
                set.ExprSelectionEnd = null;
            }
            else
            {
                set.Expression = BuildExpression(tokens);

                // Clamp selection to new token count
                if (set.ExprSelectionStart.HasValue && set.ExprSelectionEnd.HasValue)
                {
                    var start = Math.Clamp(set.ExprSelectionStart.Value, 0, tokens.Count - 1);
                    var end = Math.Clamp(set.ExprSelectionEnd.Value, 0, tokens.Count - 1);
                    if (start > end)
                        (start, end) = (end, start);
                    set.ExprSelectionStart = start;
                    set.ExprSelectionEnd = end;
                }
            }
        }
    }

    private static bool DrawGroupHeader(ConditionSet set, ConditionGroup group, int index, ConditionScope scope)
    {
        var mode = group.CombineMode;
        var btn = mode == ConditionCombineMode.All ? "&&" : "||";
        if (ImGui.Button(btn))
            group.CombineMode = mode == ConditionCombineMode.All ? ConditionCombineMode.Any : ConditionCombineMode.All;
        ImGui.TooltipOnHover($"Evaluate: {(mode is ConditionCombineMode.All ? "AND" : "OR")} - {(mode is ConditionCombineMode.All ? "all" : "any")} must be true");

        // Add condition button on same line
        ImGui.SameLine();
        if (ImGuiComponents.IconButton(FontAwesomeIcon.PlusCircle))
        {
            group.Conditions.Add(new Condition
            {
                TypeId = GetScopedTypes(scope).FirstOrDefault()?.Id ?? "StatusActive",
                Params = []
            });
        }

        // Delete/clear group button on same line
        ImGui.SameLine();
        var io = ImGui.GetIO();
        if (ImGuiComponents.IconButton(FontAwesomeIcon.Trash))
        {
            if (io.KeyShift)
            {
                // Signal to caller to delete this group entirely
                return true;
            }

            // No Shift: just clear conditions in this group
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
            DrawParams(cond);

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
        var all = Conditions.Conditions.Registry.All;

        return scope switch
        {
            ConditionScope.Hook => all.Where(d => d.Id is "StatusActive" or "StatusStacks" or "BiteTimer" or "ChumTimer"
                                                      or "IntuitionActive" or "IntuitionTime" or "SpectralActive"
                                                      or "Gp" or "MultihookAvailable" or "Weather"
                                                      or "OceanMissionType" or "OceanMissionProgress"
                                                      or "SwimbaitCount"),
            ConditionScope.AutoCordial => all.Where(d => d.Id is "StatusActive" or "Gp"),
            ConditionScope.FishIgnore => all.Where(d => d.Id is "StatusActive" or "IntuitionActive" or "IntuitionTime"
                                                             or "SpectralActive" or "Weather"
                                                             or "OceanMissionType" or "OceanMissionProgress"),
            _ => all
        };
    }

    private static void DrawParams(Condition cond)
    {
        switch (cond.TypeId)
        {
            case "StatusActive":
                DrawStatusIdParam(cond);
                break;
            case "StatusStacks":
                DrawStatusStacksParams(cond);
                break;
            case "Gp":
                DrawGpParams(cond);
                break;
            case "SwimbaitCount":
                DrawSwimbaitCountParams(cond);
                break;
            case "BiteTimer":
            case "ChumTimer":
                DrawRangeParams(cond);
                break;
            case "IntuitionTime":
                DrawIntuitionTimeParams(cond);
                break;
            case "Weather":
                DrawWeatherParams(cond);
                break;
            case "ActionAvailable":
                DrawActionAvailableParams(cond);
                break;
            case "OceanMissionType":
                DrawMissionTypeParams(cond);
                break;
            case "OceanMissionProgress":
                DrawMissionProgressParams(cond);
                break;
            default:
                break;
        }
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

    private static void DrawStatusStacksParams(Condition cond)
    {
        DrawStatusIdParam(cond);
        ImGui.SameLine();
        var minStacks = GetInt(cond.Params, "minStacks", 1);
        ImGui.SetNextItemWidth(60 * ImGuiHelpers.GlobalScale);
        if (ImGui.InputInt("Stacks", ref minStacks))
        {
            minStacks = Math.Max(1, minStacks);
            cond.Params["minStacks"] = (long)minStacks;
        }

        ImGui.SameLine();
        var op = cond.Params.TryGetValue("op", out var o) ? o?.ToString() ?? ">=" : ">=";
        var label = op is ">" or "<" or "<=" or "=" ? op : ">=";
        ImGui.SetNextItemWidth(50 * ImGuiHelpers.GlobalScale);
        using var combo = ImRaii.Combo("##stacks_op", label);
        if (combo)
        {
            foreach (var choice in new[] { ">", ">=", "<", "<=", "=" })
            {
                var sel = choice == op;
                if (ImGui.Selectable(choice, sel))
                    cond.Params["op"] = choice;
            }
        }
    }

    private static void DrawStatusIdParam(Condition cond)
    {
        var ids = GetIds(cond.Params);
        var currentId = ids.Count > 0 ? ids[0] : 0;

        var label = currentId != 0
            ? $"{currentId}: {MultiString.GetStatusName(currentId)}"
            : "Select status";

        ImGui.SetNextItemWidth(180 * ImGuiHelpers.GlobalScale);
        using var combo = ImRaii.Combo("Status", label);
        if (!combo)
            return;

        foreach (var field in typeof(IDs.Status).GetFields())
        {
            if (field.GetValue(null) is not uint id || id == 0) continue;
            var name = MultiString.GetStatusName(id);
            var isSel = id == currentId;
            if (ImGui.Selectable($"{id}: {name}", isSel))
            {
                currentId = id;
                cond.Params["ids"] = new List<object> { (long)id };
            }
        }
    }

    private static void DrawIntuitionTimeParams(Condition cond)
    {
        var sec = GetInt(cond.Params, "sec", 0);
        ImGui.SetNextItemWidth(80 * ImGuiHelpers.GlobalScale);
        if (ImGui.InputInt("Seconds", ref sec))
        {
            sec = Math.Max(0, sec);
            cond.Params["sec"] = (long)sec;
        }

        ImGui.SameLine();
        var op = cond.Params.TryGetValue("op", out var o) ? o?.ToString() ?? ">=" : ">=";
        var label = op is ">" or "<" or "<=" or "=" ? op : ">=";
        ImGui.SetNextItemWidth(50 * ImGuiHelpers.GlobalScale);
        using var combo = ImRaii.Combo("##intu_op", label);
        if (combo)
        {
            foreach (var choice in new[] { ">", ">=", "<", "<=", "=" })
            {
                var sel = choice == op;
                if (ImGui.Selectable(choice, sel))
                    cond.Params["op"] = choice;
            }
        }
    }

    private static void DrawIdsParams(Condition cond, string label)
    {
        var ids = GetIds(cond.Params);
        var text = string.Join(", ", ids);
        var buf = text;
        ImGui.SetNextItemWidth(140 * ImGuiHelpers.GlobalScale);
        if (ImGui.InputText(label, ref buf, 128))
        {
            var list = new List<object>();
            foreach (var part in buf.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                if (uint.TryParse(part.Trim(), out var id))
                    list.Add((long)id);
            }
            if (list.Count > 0)
                cond.Params["ids"] = list;
            else
                cond.Params.Remove("ids");
        }
    }

    private static void DrawGpParams(Condition cond)
    {
        var val = GetInt(cond.Params, "val", 0);
        ImGui.SetNextItemWidth(80 * ImGuiHelpers.GlobalScale);
        if (ImGui.InputInt("GP", ref val))
        {
            cond.Params["val"] = (long)val;
        }

        ImGui.SameLine();
        var op = cond.Params.TryGetValue("op", out var o) ? o?.ToString() ?? ">=" : ">=";
        var label = op is ">" or "<" or "<=" or "=" ? op : ">=";
        ImGui.SetNextItemWidth(50 * ImGuiHelpers.GlobalScale);
        if (ImGui.BeginCombo("##gp_op", label))
        {
            foreach (var choice in new[] { ">", ">=", "<", "<=", "=" })
            {
                var sel = choice == op;
                if (ImGui.Selectable(choice, sel))
                    cond.Params["op"] = choice;
            }
            ImGui.EndCombo();
        }
    }

    private static void DrawSwimbaitCountParams(Condition cond)
    {
        var val = GetInt(cond.Params, "val", 0);
        ImGui.SetNextItemWidth(80 * ImGuiHelpers.GlobalScale);
        if (ImGui.InputInt("Swimbaits", ref val))
        {
            cond.Params["val"] = (long)val;
        }

        ImGui.SameLine();
        var op = cond.Params.TryGetValue("op", out var o) ? o?.ToString() ?? ">=" : ">=";
        var label = op is ">" or "<" or "<=" or "=" ? op : ">=";
        ImGui.SetNextItemWidth(50 * ImGuiHelpers.GlobalScale);
        if (ImGui.BeginCombo("##swimbait_op", label))
        {
            foreach (var choice in new[] { ">", ">=", "<", "<=", "=" })
            {
                var sel = choice == op;
                if (ImGui.Selectable(choice, sel))
                    cond.Params["op"] = choice;
            }
            ImGui.EndCombo();
        }
    }

    private static void DrawRangeParams(Condition cond)
    {
        var ranges = GetRanges(cond.Params);
        var min = ranges.Count > 0 ? ranges[0].min : 0;
        var max = ranges.Count > 0 ? ranges[0].max : 0;

        ImGui.SetNextItemWidth(80 * ImGuiHelpers.GlobalScale);
        if (ImGui.InputDouble("Min", ref min, 0.1, 1, "%.1f"))
        {
            UpdateFirstRange(cond, min, max);
        }

        ImGui.SameLine();
        ImGui.SetNextItemWidth(80 * ImGuiHelpers.GlobalScale);
        if (ImGui.InputDouble("Max (0 = no cap)", ref max, 0.1, 1, "%.1f"))
        {
            UpdateFirstRange(cond, min, max);
        }
    }

    private static void DrawActionAvailableParams(Condition cond)
    {
        var id = (int)GetUInt(cond.Params, "id", 0);
        ImGui.SetNextItemWidth(80 * ImGuiHelpers.GlobalScale);
        if (ImGui.InputInt("Action ID", ref id))
        {
            cond.Params["id"] = (long)id;
        }

        ImGui.SameLine();
        var type = GetInt(cond.Params, "type", 0);
        var label = type switch
        {
            1 => "Item",
            2 => "Event",
            _ => "Action"
        };

        if (ImGui.BeginCombo("##act_type", label))
        {
            if (ImGui.Selectable("Action", type == 0)) type = 0;
            if (ImGui.Selectable("Item", type == 1)) type = 1;
            if (ImGui.Selectable("Event", type == 2)) type = 2;
            ImGui.EndCombo();
        }

        cond.Params["type"] = (long)type;
    }

    private static void UpdateFirstRange(Condition cond, double min, double max)
    {
        if (!cond.Params.TryGetValue("r", out var obj) || obj is not List<object> list || list.Count < 2)
        {
            cond.Params["r"] = new List<object> { min, max };
            return;
        }

        list[0] = min;
        list[1] = max;
    }

    private static List<uint> GetIds(IReadOnlyDictionary<string, object> p)
    {
        if (!p.TryGetValue("ids", out var o)) return [];
        if (o is List<object> list)
            return [.. list.Select(x => Convert.ToUInt32(x))];
        if (o is long single) return [(uint)single];
        return [];
    }

    private static bool GetBool(IReadOnlyDictionary<string, object> p, string key, bool def)
    {
        if (!p.TryGetValue(key, out var o)) return def;
        if (o is bool b) return b;
        if (o is long l) return l != 0;
        return def;
    }

    private static int GetInt(IReadOnlyDictionary<string, object> p, string key, int def)
    {
        if (!p.TryGetValue(key, out var o)) return def;
        return Convert.ToInt32(o);
    }

    private static uint GetUInt(IReadOnlyDictionary<string, object> p, string key, uint def)
    {
        if (!p.TryGetValue(key, out var o)) return def;
        return Convert.ToUInt32(o);
    }

    private static List<(double min, double max)> GetRanges(IReadOnlyDictionary<string, object> p)
    {
        if (!p.TryGetValue("r", out var o) || o is not List<object> list) return [];
        var result = new List<(double, double)>();
        for (var i = 0; i + 1 < list.Count; i += 2)
            result.Add((Convert.ToDouble(list[i]), Convert.ToDouble(list[i + 1])));
        return result;
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

    private enum ExprTokenKind
    {
        Group,
        And,
        Or,
        LParen,
        RParen,
    }

    private readonly record struct ExprToken(ExprTokenKind Kind, int GroupIndex = 0);

    private static List<ExprToken> ParseExpressionTokens(string? expr, int groupCount)
    {
        var tokens = new List<ExprToken>();
        if (string.IsNullOrWhiteSpace(expr))
            return tokens;

        var s = expr;
        var len = s.Length;
        var pos = 0;

        void SkipWs()
        {
            while (pos < len && char.IsWhiteSpace(s[pos])) pos++;
        }

        bool Match(string token)
        {
            SkipWs();
            if (pos + token.Length > len) return false;
            for (var i = 0; i < token.Length; i++)
            {
                if (s[pos + i] != token[i])
                    return false;
            }
            pos += token.Length;
            return true;
        }

        while (pos < len)
        {
            SkipWs();
            if (pos >= len) break;

            if (Match("&&"))
            {
                tokens.Add(new ExprToken(ExprTokenKind.And));
                continue;
            }

            if (Match("||"))
            {
                tokens.Add(new ExprToken(ExprTokenKind.Or));
                continue;
            }

            var ch = s[pos];
            if (ch == '(')
            {
                tokens.Add(new ExprToken(ExprTokenKind.LParen));
                pos++;
                continue;
            }

            if (ch == ')')
            {
                tokens.Add(new ExprToken(ExprTokenKind.RParen));
                pos++;
                continue;
            }

            if (char.IsLetter(ch))
            {
                var c = char.ToUpperInvariant(ch);
                var idx = c - 'A';
                if (idx >= 0 && idx < groupCount)
                    tokens.Add(new ExprToken(ExprTokenKind.Group, idx));
                pos++;
                continue;
            }

            // Unknown character, skip
            pos++;
        }

        return tokens;
    }

    private static bool[] ValidateExpressionTokens(List<ExprToken> tokens)
    {
        var invalid = new bool[tokens.Count];
        if (tokens.Count == 0) return invalid;

        var last = ExprTokenKind.LParen; // treat "none" as "expect operand"
        var depth = 0;

        for (var i = 0; i < tokens.Count; i++)
        {
            var t = tokens[i];
            switch (t.Kind)
            {
                case ExprTokenKind.Group:
                    // Invalid if previous was also operand or right paren
                    if (last is ExprTokenKind.Group or ExprTokenKind.RParen)
                        invalid[i] = true;
                    last = ExprTokenKind.Group;
                    break;

                case ExprTokenKind.And:
                case ExprTokenKind.Or:
                    // Invalid at start, after operator, or after '('
                    if (i == 0 || last is ExprTokenKind.And or ExprTokenKind.Or or ExprTokenKind.LParen)
                        invalid[i] = true;
                    last = t.Kind;
                    break;

                case ExprTokenKind.LParen:
                    // Invalid directly after operand or ')'
                    if (last is ExprTokenKind.Group or ExprTokenKind.RParen)
                        invalid[i] = true;
                    depth++;
                    last = ExprTokenKind.LParen;
                    break;

                case ExprTokenKind.RParen:
                    // Invalid if nothing to match or if previous wasn't an operand/closing paren
                    if (depth <= 0 || last is ExprTokenKind.And or ExprTokenKind.Or or ExprTokenKind.LParen)
                        invalid[i] = true;
                    else
                        depth--;
                    last = ExprTokenKind.RParen;
                    break;
            }
        }

        // Trailing operator or '(' is invalid
        if (tokens.Count > 0)
        {
            var lastIdx = tokens.Count - 1;
            if (tokens[lastIdx].Kind is ExprTokenKind.And or ExprTokenKind.Or or ExprTokenKind.LParen)
                invalid[lastIdx] = true;
        }

        // Unmatched '(' – mark from right to left until depth is satisfied
        if (depth > 0)
        {
            for (var i = tokens.Count - 1; i >= 0 && depth > 0; i--)
            {
                if (tokens[i].Kind == ExprTokenKind.LParen)
                {
                    invalid[i] = true;
                    depth--;
                }
            }
        }

        return invalid;
    }

    private static string GetTokenLabel(ExprToken token)
    {
        return token.Kind switch
        {
            ExprTokenKind.Group => ((char)('A' + token.GroupIndex)).ToString(),
            ExprTokenKind.And => "&&",
            ExprTokenKind.Or => "||",
            ExprTokenKind.LParen => "(",
            ExprTokenKind.RParen => ")",
            _ => "?",
        };
    }

    private static string BuildExpression(List<ExprToken> tokens)
    {
        if (tokens.Count == 0)
            return string.Empty;

        var sb = new StringBuilder();
        for (var i = 0; i < tokens.Count; i++)
        {
            if (i > 0)
                sb.Append(' ');
            sb.Append(GetTokenLabel(tokens[i]));
        }

        return sb.ToString();
    }

    private static void DrawWeatherParams(Condition cond)
    {
        var ids = GetIds(cond.Params);
        var currentId = ids.Count > 0 ? (byte)ids[0] : (byte)0;

        ImGui.SetNextItemWidth(180 * ImGuiHelpers.GlobalScale);
        var sheet = Svc.Data.GetExcelSheet<Lumina.Excel.Sheets.Weather>();
        if (sheet == null)
        {
            DrawIdsParams(cond, "Weather IDs");
            return;
        }

        // unique list by weather name so duplicates collapse.
        var unique = new Dictionary<string, byte>();
        foreach (var row in sheet)
        {
            var name = row.Name.ToString();
            if (string.IsNullOrEmpty(name)) continue;
            if (!unique.ContainsKey(name))
                unique[name] = (byte)row.RowId;
        }

        string label;
        if (currentId != 0 && sheet.TryGetRow(currentId, out var currentRow))
            label = currentRow.Name.ToString();
        else
            label = "Any weather";

        if (ImGui.BeginCombo("Weather", label))
        {
            foreach (var kv in unique.OrderBy(k => k.Key))
            {
                var id = kv.Value;
                var name = kv.Key;
                var sel = id == currentId;
                if (ImGui.Selectable(name, sel))
                {
                    currentId = id;
                    cond.Params["ids"] = new List<object> { (long)id };
                }
            }

            ImGui.EndCombo();
        }
    }

    private static void DrawMissionTypeParams(Condition cond)
    {
        var ids = GetIds(cond.Params);
        var currentId = ids.Count > 0 ? ids[0] : 0;

        ImGui.SetNextItemWidth(220 * ImGuiHelpers.GlobalScale);
        var sheet = Svc.Data.GetExcelSheet<Lumina.Excel.Sheets.IKDPlayerMissionCondition>();
        if (sheet == null)
        {
            DrawIdsParams(cond, "Mission type IDs");
            return;
        }

        string LabelForRow(uint rowId)
        {
            if (rowId == 0) return "Select mission";
            if (!sheet.TryGetRow(rowId, out var row)) return $"{rowId}";
            var name = MultiString.ParseSeString(row.Unknown0);
            return string.IsNullOrEmpty(name) ? $"{rowId}" : $"{rowId}: {name}";
        }

        var label = LabelForRow(currentId);
        if (ImGui.BeginCombo("Mission type", label))
        {
            foreach (var row in sheet)
            {
                var name = MultiString.ParseSeString(row.Unknown0);
                if (string.IsNullOrEmpty(name)) continue;
                var id = row.RowId;
                var sel = id == currentId;
                if (ImGui.Selectable($"{id}: {name}", sel))
                {
                    currentId = id;
                    cond.Params["ids"] = new List<object> { (long)id };
                }
            }
            ImGui.EndCombo();
        }
    }

    private static void DrawMissionProgressParams(Condition cond)
    {
        var mission = GetInt(cond.Params, "mission", 1);
        mission = Math.Clamp(mission, 1, 3);

        ImGui.SetNextItemWidth(60 * ImGuiHelpers.GlobalScale);
        if (ImGui.BeginCombo("Mission", $"{mission}"))
        {
            if (ImGui.Selectable("1", mission == 1)) { mission = 1; cond.Params["mission"] = (long)1; }
            if (ImGui.Selectable("2", mission == 2)) { mission = 2; cond.Params["mission"] = (long)2; }
            if (ImGui.Selectable("3", mission == 3)) { mission = 3; cond.Params["mission"] = (long)3; }
            ImGui.EndCombo();
        }

        ImGui.SameLine();
        var val = GetInt(cond.Params, "val", 0);
        ImGui.SetNextItemWidth(70 * ImGuiHelpers.GlobalScale);
        if (ImGui.InputInt("Progress", ref val))
        {
            val = Math.Max(0, val);
            cond.Params["val"] = (long)val;
        }

        ImGui.SameLine();
        var op = cond.Params.TryGetValue("op", out var o) ? o?.ToString() ?? ">=" : ">=";
        var label = op is ">" or "<" or "<=" or "=" ? op : ">=";
        ImGui.SetNextItemWidth(50 * ImGuiHelpers.GlobalScale);
        if (ImGui.BeginCombo("##mission_op", label))
        {
            foreach (var choice in new[] { ">", ">=", "<", "<=", "=" })
            {
                var sel = choice == op;
                if (ImGui.Selectable(choice, sel))
                    cond.Params["op"] = choice;
            }
            ImGui.EndCombo();
        }
    }

    /// <summary>
    /// Add a few quick preset buttons for simple cases per scope.
    /// These manipulate the first group in the set (creating it if needed).
    /// </summary>
    private static void DrawScopePresets(ConditionScope scope, ConditionSet set, ConditionGroup group)
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
        }
    }

    private static void DrawHookPresets(ConditionSet set, ConditionGroup group)
    {
        ImGui.TextColored(ImGuiColors.DalamudGrey, "Presets:");

        if (DrawActionIconButton(IDs.Actions.SurfaceSlap,
                ImGui.GetIO().KeyShift
                    ? "Require Surface Slap NOT active (Shift held)"
                    : "Require Surface Slap active (hold Shift for NOT active)"))
        {
            var inverse = ImGui.GetIO().KeyShift;
            AddStatusPreset(set, group, IDs.Status.SurfaceSlap, inverse);
        }

        ImGui.SameLine();

        if (DrawActionIconButton(IDs.Actions.IdenticalCast,
                ImGui.GetIO().KeyShift
                    ? "Require Identical Cast NOT active (Shift held)"
                    : "Require Identical Cast active (hold Shift for NOT active)"))
        {
            var inverse = ImGui.GetIO().KeyShift;
            AddStatusPreset(set, group, IDs.Status.IdenticalCast, inverse);
        }

        ImGui.SameLine();

        if (DrawActionIconButton(IDs.Actions.PrizeCatch,
                ImGui.GetIO().KeyShift
                    ? "Require Prize Catch NOT active (Shift held)"
                    : "Require Prize Catch active (hold Shift for NOT active)"))
        {
            var inverse = ImGui.GetIO().KeyShift;
            AddStatusPreset(set, group, IDs.Status.PrizeCatch, inverse);
        }

        ImGui.SameLine();

        if (DrawActionIconButton(IDs.Actions.MultiHook, "Require Multihook available"))
            AddMultihookPreset(set, group);
    }

    private static void DrawCordialPresets(ConditionSet set, ConditionGroup group)
    {
        ImGui.TextColored(ImGuiColors.DalamudGrey, "Presets:");

        if (DrawActionIconButton(IDs.Actions.IdenticalCast,
                ImGui.GetIO().KeyShift
                    ? "Allow overcap when Identical Cast is NOT active (Shift held)"
                    : "Allow overcap when Identical Cast is active (hold Shift for NOT active)"))
        {
            var inverse = ImGui.GetIO().KeyShift;
            AddStatusPreset(set, group, IDs.Status.IdenticalCast, inverse);
        }
    }

    private static void DrawFishPresets(ConditionSet set, ConditionGroup group)
    {
        ImGui.TextColored(ImGuiColors.DalamudGrey, "Presets:");

        if (DrawStatusIconButton(IDs.Status.FishersIntuition, "Ignore when Fisher's Intuition is active"))
        {
            if (!group.Conditions.Any(c => c.TypeId == "IntuitionActive"))
            {
                group.Conditions.Add(new Condition
                {
                    TypeId = "IntuitionActive",
                    Params = []
                });
            }
        }
    }

    private static void AddStatusPreset(ConditionSet set, ConditionGroup group, uint statusId, bool inverse)
    {

        foreach (var c in group.Conditions.Where(c => c.TypeId == "StatusActive"))
        {
            var ids = GetIds(c.Params);
            var inv = GetBool(c.Params, "inv", false);
            if (ids.Contains(statusId) && inv == inverse)
                return; // already present
        }

        var cond = new Condition
        {
            TypeId = "StatusActive",
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
        if (group.Conditions.Any(c => c.TypeId == "MultihookAvailable"))
            return;

        group.Conditions.Add(new Condition
        {
            TypeId = "MultihookAvailable",
            Params = []
        });
    }

    private static bool DrawStatusIconButton(uint statusId, string? tooltip = null)
    {
        var iconId = GetRow<Lumina.Excel.Sheets.Status>(statusId)?.Icon ?? 0;
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
        var iconId = GetRow<Lumina.Excel.Sheets.Action>(actionId)?.Icon ?? 0u;
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

