using AutoHook.Conditions;
using AutoHook.Spearfishing.Enums;
using AutoHook.Ui;
using Dalamud.Bindings.ImGui;
using System.ComponentModel;

namespace AutoHook.Classes;

public class BaseGig(int itemId) : BaseOption {
    [DefaultValue(true)]
    public bool Enabled = true;

    private int _itemId = itemId;
    public ImportedFish? Fish {
        get {
            if (field == null && _itemId != 0 &&
                GameRes.SpearfishFishesByItemId.TryGetValue((uint)_itemId, out var fish))
                field = fish;
            return field;
        }
        set {
            field = value;
            _itemId = value?.ItemId ?? 0;
        }
    } = GameRes.SpearfishFishesByItemId.GetValueOrDefault((uint)itemId);

    [DefaultValue(0u)]
    public uint SpearfishingNotebookId { get; set; }

    public bool IsAnyPool => SpearfishingNotebookId == 0;

    public ConditionSet? GigConditionSet { get; set; }
    public AutoNaturesBounty NaturesBounty { get; set; } = new(true);

    public float LeftOffset;
    public float RightOffset;

    public SpearfishSpeed Speed => Fish?.Speed ?? SpearfishSpeed.Unknown;
    public SpearfishSize Size => Fish?.Size ?? SpearfishSize.Unknown;

    public override void DrawOptions() {
        var choices = SpearfishingNotebookId == 0 || !GameRes.SpearfishingPoolsByNotebookId.TryGetValue(SpearfishingNotebookId, out var pool)
            ? GameRes.SpearfishFishes : [.. pool.ItemIds.Select(id => GameRes.SpearfishFishesByItemId.GetValueOrDefault(id)).Where(fish => fish != null).Select(fish => fish!)];
        DrawUtil.DrawComboSelector(choices, item => item.Name, Fish?.Name ?? UIStrings.None, item => Fish = item);

        GigConditionSet = ConditionUi.DrawConditionSet(UIStrings.Conditions, GigConditionSet, ConditionScope.Spearfishing, showAdvanced: true);
        NaturesBounty.DrawConfig();

        DrawUtil.DrawTreeNodeEx(UIStrings.Fish_Hitbox_Offset, () => {
            var x = ImGui.GetCursorPosX();
            ImGui.SetCursorPosX(x);
            if (DrawUtil.EditFloatField(UIStrings.OffsetLR, ref LeftOffset,
                    UIStrings.OffsetLRHelpText, true)) {
                LeftOffset = Math.Max(-10, Math.Min(LeftOffset, 10));
                Service.Save();
            }

            ImGui.SetCursorPosX(x);
            if (DrawUtil.EditFloatField(UIStrings.OffsetRL, ref RightOffset,
                    UIStrings.OffsetRLHelpText, true)) {
                RightOffset = Math.Max(-10, Math.Min(RightOffset, 10));
                Service.Save();
            }
        }, UIStrings.FishHitboxHelpText);

    }

    public override bool Equals(object? obj) {
        return obj is BaseGig settings && Fish?.ItemId == settings.Fish?.ItemId && SpearfishingNotebookId == settings.SpearfishingNotebookId;
    }

    public override int GetHashCode() {
        return HashCode.Combine(Fish?.ItemId, SpearfishingNotebookId);
    }
}
