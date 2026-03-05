using System.Text;
using AutoHook.Conditions;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility.Raii;

namespace AutoHook.Ui;

public static class ConditionExpressionUi
{
    internal enum ExprTokenKind
    {
        Group,
        And,
        Or,
        LParen,
        RParen,
    }

    internal readonly record struct ExprToken(ExprTokenKind Kind, int GroupIndex = 0);

    public static void DrawExpressionEditor(ConditionSet set)
    {
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

        ImGui.TextColored(ImGuiColors.DalamudGrey, $"{UIStrings.Add}:");
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

    internal static List<ExprToken> ParseExpressionTokens(string? expr, int groupCount)
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

            pos++;
        }

        return tokens;
    }

    internal static bool[] ValidateExpressionTokens(List<ExprToken> tokens)
    {
        var invalid = new bool[tokens.Count];
        if (tokens.Count == 0) return invalid;

        var last = ExprTokenKind.LParen;
        var depth = 0;

        for (var i = 0; i < tokens.Count; i++)
        {
            var t = tokens[i];
            switch (t.Kind)
            {
                case ExprTokenKind.Group:
                    if (last is ExprTokenKind.Group or ExprTokenKind.RParen)
                        invalid[i] = true;
                    last = ExprTokenKind.Group;
                    break;

                case ExprTokenKind.And:
                case ExprTokenKind.Or:
                    if (i == 0 || last is ExprTokenKind.And or ExprTokenKind.Or or ExprTokenKind.LParen)
                        invalid[i] = true;
                    last = t.Kind;
                    break;

                case ExprTokenKind.LParen:
                    if (last is ExprTokenKind.Group or ExprTokenKind.RParen)
                        invalid[i] = true;
                    depth++;
                    last = ExprTokenKind.LParen;
                    break;

                case ExprTokenKind.RParen:
                    if (depth <= 0 || last is ExprTokenKind.And or ExprTokenKind.Or or ExprTokenKind.LParen)
                        invalid[i] = true;
                    else
                        depth--;
                    last = ExprTokenKind.RParen;
                    break;
            }
        }

        if (tokens.Count > 0)
        {
            var lastIdx = tokens.Count - 1;
            if (tokens[lastIdx].Kind is ExprTokenKind.And or ExprTokenKind.Or or ExprTokenKind.LParen)
                invalid[lastIdx] = true;
        }

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

    internal static string GetTokenLabel(ExprToken token)
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

    internal static string BuildExpression(List<ExprToken> tokens)
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
}

