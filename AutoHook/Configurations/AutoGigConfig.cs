using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Bindings.ImGui;

namespace AutoHook.Configurations;

public class AutoGigConfig : BasePresetConfig
{
    public string Name { get; set; } = "Old Preset";

    public List<BaseGig> Gigs { get; set; } = [];

    public int HitboxSize = 25;

    public AutoGigConfig(string presetName)
    {
        PresetName = presetName;
    }

    public List<BaseGig> GetGigCurrentNode(int node)
    {
        Service.PrintDebug($"[AutoGig] GetGigCurrentNode - node: {node}, Total Gigs: {Gigs?.Count ?? 0}");
        
        var result = Gigs.Where(f =>
        {
            var hasFish = f.Fish != null;
            var hasNode = f.Fish?.Nodes.Contains(node) ?? false;
            Service.PrintDebug($"[AutoGig] GetGigCurrentNode - Fish: {f.Fish?.Name ?? "null"}, Enabled: {f.Enabled}, HasFish: {hasFish}, HasNode: {hasNode}");
            return hasFish && hasNode;
        }).ToList();
        
        Service.PrintDebug($"[AutoGig] GetGigCurrentNode - Returning {result.Count} fish(es)");
        return result;
    }

    public override void AddItem(BaseOption item)
    {
        Gigs.Add((BaseGig)item);
        Service.Save();
    }

    public override void RemoveItem(Guid value)
    {
        Gigs.RemoveAll(x => x.UniqueId == value);
        Service.Save();
    }

    public override void DrawOptions()
    {
        if (Gigs == null || Gigs.Count == 0)
            return;

        foreach (var gig in Gigs)
        {
            ImGui.PushID(gig.UniqueId.ToString());
            using (ImRaii.PushFont(UiBuilder.IconFont))
            {
                var icon = FontAwesomeIcon.Trash.ToIconString();
                var buttonSize = ImGui.CalcTextSize(icon) + ImGui.GetStyle().FramePadding * 2;
                if (ImGui.Button(@$"{icon}", buttonSize) &&
                    ImGui.GetIO().KeyShift)
                {
                    RemoveItem(gig.UniqueId);
                    Service.Save();
                    return;
                }
            }

            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(UIStrings.HoldShiftToDelete);

            ImGui.SameLine(0, 3);

            DrawUtil.Checkbox(@$"", ref gig.Enabled);

            ImGui.SameLine(0, 3);

            var x = ImGui.GetCursorPosX();
            if (ImGui.TreeNodeEx($"{gig.Fish?.Name ?? UIStrings.None}", ImGuiTreeNodeFlags.FramePadding))
            {
                ImGui.SetCursorPosX(x);
                ImGui.BeginGroup();
                gig.DrawOptions();
                ImGui.EndGroup();
                ImGui.TreePop();
            }

            ImGui.PopID();
        }
    }
}