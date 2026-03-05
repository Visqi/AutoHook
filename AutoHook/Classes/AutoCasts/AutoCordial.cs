using AutoHook.Conditions;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace AutoHook.Classes.AutoCasts;

public class AutoCordial : BaseActionCast
{
    private const uint CordialHiRecovery = 400;
    private const uint CordialHqRecovery = 350;
    private const uint CordialRecovery = 300;
    private const uint CordialHqWateredRecovery = 200;
    private const uint CordialWateredRecovery = 150;

    public bool InvertCordialPriority;

    [Obsolete("Legacy config. Replaced by ConditionSet.")] public bool AllowOvercapIC;

    public bool IgnoreTimeWindow;

    public ConditionSet? OvercapConditionSet { get; set; }

    public override bool RequiresTimeWindow() => !IgnoreTimeWindow;

    [NonSerialized]
    public readonly List<(uint, uint)> _cordialList =
    [
        (IDs.Item.HiCordial,        CordialHiRecovery),
        (IDs.Item.HQCordial,        CordialHqRecovery),
        (IDs.Item.Cordial,          CordialRecovery),
        (IDs.Item.HQWateredCordial, CordialHqWateredRecovery),
        (IDs.Item.WateredCordial,   CordialWateredRecovery)
    ];

    [NonSerialized]
    private readonly List<(uint, uint)> _invertedList =
    [
        (IDs.Item.WateredCordial,   CordialWateredRecovery),
        (IDs.Item.HQWateredCordial, CordialHqWateredRecovery),
        (IDs.Item.Cordial,          CordialRecovery),
        (IDs.Item.HQCordial,        CordialHqRecovery),
        (IDs.Item.HiCordial,        CordialHiRecovery)
    ];

    public AutoCordial(bool isSpearFishing = false) : base(UIStrings.Cordial, IDs.Item.Cordial, ActionType.Item)
    {
        IsSpearFishing = isSpearFishing;
    }

    public override string GetName()
        => Name = UIStrings.Cordial;
    public override bool CastCondition()
    {
        var cordialList = _cordialList;

        if (InvertCordialPriority)
            cordialList = _invertedList;

        foreach (var (id, recovery) in cordialList)
        {
            if (!Service.WorldState.HaveCordialInInventory(id))
                continue;

            Id = id;

            return CheckNotOvercaped(recovery);
        }

        return false;
    }

    public override void SetThreshold(int newCost)
    {
        if (newCost <= 0)
            GpThreshold = 0;
        else
            GpThreshold = newCost;
    }

    private bool CheckNotOvercaped(uint recovery)
    {
        if (OvercapConditionSet is { Groups.Count: > 0 } &&
            OvercapConditionSet.Evaluate(Service.WorldState, Conditions.Conditions.Registry))
            return true;

        return Service.WorldState.CurrentGp + recovery <= Service.WorldState.MaxGp;
    }

    protected override DrawOptionsDelegate DrawOptions => () =>
    {
        if (DrawUtil.Checkbox(UIStrings.AutoCastCordialPriority, ref InvertCordialPriority))
            Service.Save();

        if (!IsSpearFishing)
        {
            if (DrawUtil.Checkbox(UIStrings.CordialOutsideTimeWindow, ref IgnoreTimeWindow, UIStrings.CordialOutsideTimeWindowHelpText))
                Service.Save();

            OvercapConditionSet = Ui.ConditionUi.DrawConditionSet("Overcap conditions", OvercapConditionSet, Ui.ConditionScope.AutoCordial);
        }
    };

    public override int Priority { get; set; } = 4;
    public override bool IsExcludedPriority { get; set; } = false;
}
