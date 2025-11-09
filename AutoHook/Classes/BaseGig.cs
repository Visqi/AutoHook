using System.ComponentModel;
using AutoHook.Spearfishing.Enums;

namespace AutoHook.Classes;

public class BaseGig(int itemId) : BaseOption
{
    [DefaultValue(true)]
    public bool Enabled = true;

    public ImportedFish? Fish = GameRes.ImportedFishes.FirstOrDefault(f => f.ItemId == itemId);

    public bool UseNaturesBounty;

    public float LeftOffset;
    public float RightOffset;

    public SpearfishSpeed Speed => Fish?.Speed ?? SpearfishSpeed.Unknown;
    public SpearfishSize Size => Fish?.Size ?? SpearfishSize.Unknown;

    public override void DrawOptions()
    {
        DrawUtil.DrawComboSelector(
            [.. GameRes.ImportedFishes.Where(f => f.IsSpearFish)],
            (ImportedFish item) => item.Name,
            Fish?.Name ?? UIStrings.None,
            (ImportedFish item) => Fish = item);

        DrawUtil.Checkbox(UIStrings.UseNaturesBounty, ref UseNaturesBounty);

        DrawUtil.DrawTreeNodeEx(UIStrings.Fish_Hitbox_Offset, () =>
        {
            if (DrawUtil.EditFloatField(UIStrings.OffsetLR, ref LeftOffset,
                    UIStrings.OffsetLRHelpText, true))
            {
                LeftOffset = Math.Max(-10, Math.Min(LeftOffset, 10));
                Service.Save();
            }

            if (DrawUtil.EditFloatField(UIStrings.OffsetRL, ref RightOffset,
                    UIStrings.OffsetRLHelpText, true))
            {
                RightOffset = Math.Max(-10, Math.Min(RightOffset, 10));
                Service.Save();
            }
        }, UIStrings.FishHitboxHelpText);

    }

    public override bool Equals(object? obj)
    {
        return obj is BaseGig settings &&
               Fish?.ItemId == settings.Fish?.ItemId;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(UniqueId);
    }
}