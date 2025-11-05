using AutoHook.Data;
using AutoHook.Resources.Localization;
using AutoHook.Utils;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace AutoHook.Classes.AutoCasts;

public class AutoThaliaksFavor : BaseActionCast
{
    public int ThaliaksFavorStacks = 3;
    public int ThaliaksFavorRecover = 150;
    public bool UseWhenCordialCD;

    public AutoThaliaksFavor(bool isSpearfishing = false) : base(UIStrings.Thaliaks_Favor, IDs.Actions.ThaliaksFavor, ActionType.Action)
    {
        HelpText = UIStrings.TabAutoCasts_DrawThaliaksFavor_HelpText;
        IsSpearFishing = isSpearfishing;
    }

    public override string GetName()
        => Name = UIStrings.Thaliaks_Favor;

    public override bool CastCondition()
    {
        bool allowedToUseThaliaks = true;
        bool hasStacks = PlayerRes.HasAnglersArtStacks(ThaliaksFavorStacks);

        bool notOvercaped = (PlayerRes.GetCurrentGp() + ThaliaksFavorRecover) < PlayerRes.GetMaxGp();

        if (UseWhenCordialCD)
        {
            var cordialConfig = AutoHook.Plugin.HookManager.GetAutoCastCfg().CastCordial;
            bool hasCordial = false;
            foreach (var cordial in cordialConfig._cordialList)
            {
                hasCordial |= PlayerRes.HaveCordialInInventory(cordial.Item1);
            }

            bool cordialAvailable = cordialConfig.Enabled && PlayerRes.IsPotOffCooldown() && hasCordial;

            allowedToUseThaliaks = !cordialAvailable;
        }

        return hasStacks && notOvercaped && allowedToUseThaliaks; // dont use if its going to overcap gp
    }

    protected override DrawOptionsDelegate DrawOptions => () =>
    {
        var stack = ThaliaksFavorStacks;
        if (DrawUtil.EditNumberField(UIStrings.TabAutoCasts_DrawExtraOptionsThaliaksFavor_, ref stack))
        {
            // value has to be between 3 and 10
            ThaliaksFavorStacks = Math.Max(3, Math.Min(stack, 10));
            Service.Save();
        }

        if (DrawUtil.Checkbox(UIStrings.ThaliaksCordialOffCd, ref UseWhenCordialCD, UIStrings.Use_Cordials_First_Help))
            Service.Save();
    };

    public override int Priority { get; set; } = 16;
    public override bool IsExcludedPriority { get; set; } = false;
}