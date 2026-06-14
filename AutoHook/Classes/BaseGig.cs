using AutoHook.Spearfishing.Enums;

namespace AutoHook.Classes;

public class BaseGig(int itemId) : BaseOption {
    public bool Enabled = true;

    private int _itemId = itemId;
    public ImportedFish? Fish {
        get {
            if (field == null && _itemId != 0) {
                Service.PrintDebug($"[AutoGig] BaseGig.Fish - Lazy initializing for itemId: {_itemId}, SpearfishFishes count: {GameRes.SpearfishFishes.Count}");
                field = GameRes.SpearfishFishes.FirstOrDefault(f => f.ItemId == _itemId);
                Service.PrintDebug($"[AutoGig] BaseGig.Fish - Found: {(field != null ? field.Name : "null")}");
            }
            return field;
        }
        set {
            Service.PrintDebug($"[AutoGig] BaseGig.Fish - Setting to: {(value != null ? value.Name : "null")}");
            field = value;
        }
    } = GameRes.SpearfishFishes.FirstOrDefault(f => f.ItemId == itemId);

    public bool UseNaturesBounty;

    public float LeftOffset;
    public float RightOffset;

    public SpearfishSpeed Speed => Fish?.Speed ?? SpearfishSpeed.Unknown;
    public SpearfishSize Size => Fish?.Size ?? SpearfishSize.Unknown;

    public override void DrawOptions() {
        DrawUtil.DrawComboSelector([.. GameRes.SpearfishFishes], item => item.Name, Fish?.Name ?? UIStrings.None, item => Fish = item);

        DrawUtil.Checkbox(UIStrings.UseNaturesBounty, ref UseNaturesBounty);

        DrawUtil.DrawTreeNodeEx(UIStrings.Fish_Hitbox_Offset, () => {
            if (DrawUtil.EditFloatField(UIStrings.OffsetLR, ref LeftOffset,
                    UIStrings.OffsetLRHelpText, true)) {
                LeftOffset = Math.Max(-10, Math.Min(LeftOffset, 10));
                Service.Save();
            }

            if (DrawUtil.EditFloatField(UIStrings.OffsetRL, ref RightOffset,
                    UIStrings.OffsetRLHelpText, true)) {
                RightOffset = Math.Max(-10, Math.Min(RightOffset, 10));
                Service.Save();
            }
        }, UIStrings.FishHitboxHelpText);

    }

    public override bool Equals(object? obj) {
        return obj is BaseGig settings &&
               Fish?.ItemId == settings.Fish?.ItemId;
    }

    public override int GetHashCode() {
        return HashCode.Combine(UniqueId);
    }
}
