using Dalamud.Bindings.ImGui;
using Dalamud.Game.Text;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.Game;
using static AutoHook.Conditions.IConditionDefinition;

namespace AutoHook.Conditions.Definitions;

public sealed class ActionCooldownCD : IConditionDefinition {
    public string Id => nameof(ActionCooldownCD);
    public string Name => "Action";
    public string Category => "Player";
    public string Description => "Compares the cooldown (seconds remaining) of an action or item against a value.";
    public ConditionScopeFlags AllowedScopes => ConditionScopeFlags.Hook | ConditionScopeFlags.AutoCast;

    public readonly record struct ActionCooldownParams(uint Id, int Type, int Seconds, string Op, bool Invert) {
        public Dictionary<string, object> ToParams() {
            var dict = new Dictionary<string, object>();

            if (Id != 0)
                dict["id"] = (long)Id;

            if (Type != 0)
                dict["type"] = (long)Type;

            if (Seconds != 0)
                dict["sec"] = (long)Seconds;

            if (!string.IsNullOrEmpty(Op) && Op != "=")
                dict["op"] = Op;

            if (Invert)
                dict["inv"] = true;

            return dict;
        }
    }

    public bool Evaluate(WorldState world, IReadOnlyDictionary<string, object> parameters) {
        var args = GetParams(parameters);
        if (args.Id == 0)
            return false;

        var actionType = GetActionType(args.Type);
        var cd = GetEffectiveCooldown(args.Id, actionType);
        var lhs = (int)Math.Floor(cd);
        var rhs = args.Seconds;
        var result = CompareInt(lhs, rhs, args.Op);
        return args.Invert ? !result : result;
    }

    public void DrawParams(Condition condition) {
        var args = GetParams(condition.Params);

        var typeInt = args.Type;
        var label = typeInt == 1 ? "Item" : "Action";

        ImGui.SetNextItemWidth(90 * ImGuiHelpers.GlobalScale);
        using (var comboType = ImRaii.Combo("Type", label)) {
            if (comboType.Success) {
                if (ImGui.Selectable("Action", typeInt == 0)) typeInt = 0;
                if (ImGui.Selectable("Item", typeInt == 1)) typeInt = 1;

                args = args with { Type = typeInt };
                condition.Params = args.ToParams();
            }
        }

        ImGui.SameLine();
        var currentId = args.Id;
        var idLabel = GetIdLabel(typeInt, currentId);
        ImGui.SetNextItemWidth(220 * ImGuiHelpers.GlobalScale);
        using (var comboId = ImRaii.Combo("##act_id", idLabel)) {
            if (comboId.Success) {
                switch (typeInt) {
                    case 1: // Item
                        foreach (var field in typeof(IDs.Item).GetFields()) {
                            if (field.GetValue(null) is not uint id || id == 0) continue;
                            var (baseId, itemKind) = ItemUtil.GetBaseId(id);
                            var name = MultiString.GetItemName(baseId);
                            if (string.IsNullOrEmpty(name)) continue;
                            var sel = id == currentId;
                            if (!ImGui.Selectable($"{id}: {name}{(itemKind is ItemKind.Hq ? $" {SeIconChar.HighQuality.ToIconString()}" : string.Empty)}", sel))
                                continue;

                            currentId = id;
                            args = args with { Id = id };
                            condition.Params = args.ToParams();
                        }
                        break;
                    default:
                        foreach (var field in typeof(IDs.Actions).GetFields()) {
                            if (field.GetValue(null) is not uint id || id == 0) continue;
                            var name = MultiString.GetActionName(id);
                            if (string.IsNullOrEmpty(name)) continue;
                            var sel = id == currentId;
                            if (!ImGui.Selectable($"{id}: {name}", sel))
                                continue;

                            currentId = id;
                            args = args with { Id = id };
                            condition.Params = args.ToParams();
                        }
                        break;
                }
            }
        }

        var sec = args.Seconds;
        ImGui.SetNextItemWidth(80 * ImGuiHelpers.GlobalScale);
        if (ImGui.InputInt("Cooldown (sec)", ref sec)) {
            sec = Math.Max(0, sec);
            args = args with { Seconds = sec };
            condition.Params = args.ToParams();
        }

        ImGui.SameLine();
        var opLabel = args.Op is ">" or ">=" or "<" or "<=" or "=" ? args.Op : "=";
        ImGui.SetNextItemWidth(50 * ImGuiHelpers.GlobalScale);
        using (var comboOp = ImRaii.Combo("##act_cd_op", opLabel)) {
            if (comboOp.Success) {
                foreach (var choice in new[] { ">", ">=", "<", "<=", "=" }) {
                    var sel = choice == args.Op;
                    if (!ImGui.Selectable(choice, sel))
                        continue;

                    args = args with { Op = choice };
                    condition.Params = args.ToParams();
                }
            }
        }
    }

    private static ActionCooldownParams GetParams(IReadOnlyDictionary<string, object> p) {
        var id = GetUInt(p, "id", 0);
        var type = GetInt(p, "type", 0);
        var sec = GetDouble(p, "sec", 0);
        var op = GetOp(p, "op", "=");
        var inv = GetBool(p, "inv", false);
        var secondsInt = (int)Math.Floor(sec);
        return new ActionCooldownParams(id, type, secondsInt, op, inv);
    }

    private static string GetIdLabel(int type, uint id) {
        if (id == 0) {
            return type switch {
                1 => "Select item",
                _ => "Select action",
            };
        }

        return type switch {
            1 => $"{id}: {MultiString.GetItemName(ItemUtil.GetBaseId(id).ItemId)}",
            _ => $"{id}: {MultiString.GetActionName(id)}",
        };
    }

    private static ActionType GetActionType(int type)
        => type == 1 ? ActionType.Item : ActionType.Action;

    private static float GetEffectiveCooldown(uint id, ActionType type) {
        try {
            if (PlayerRes.ActionStatus(id, type) != 0)
                return float.MaxValue;

            var cd = PlayerRes.GetCooldown(id, type);
            return cd <= 0 ? 0 : cd;
        }
        catch {
            return float.MaxValue;
        }
    }
}
