using FFXIVClientStructs.FFXIV.Client.Game;

namespace AutoHook.Classes.AutoCasts;

public class AutoSparefulHand : BaseActionCast
{
    public int SwimbaitCountLimit { get; set; } = 3;

    public AutoSparefulHand() : base(UIStrings.SparefulHand, IDs.Actions.SparefulHand, ActionType.Action)
    {
        HelpText = UIStrings.SparefulHand_HelpText;
    }

    public override string GetName()
        => Name = UIStrings.SparefulHand;

    public uint? FishIdToCheck { get; set; }

    public override bool CastCondition()
    {
        // Check swimbait count for this specific fish if limit is set
        if (SwimbaitCountLimit > 0 && FishIdToCheck.HasValue)
        {
            var currentSwimbaitCount = Service.BaitManager.GetSwimbaitCountForFish(FishIdToCheck.Value);
            if (currentSwimbaitCount >= SwimbaitCountLimit)
                return false;
        }

        return true;
    }

    public override int Priority { get; set; } = 20;
    public override bool IsExcludedPriority { get; set; } = false;
}
