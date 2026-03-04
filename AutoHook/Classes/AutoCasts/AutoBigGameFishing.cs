namespace AutoHook.Classes.AutoCasts;

public class AutoBigGameFishing : BaseActionCast
{
    public int AnglersStacks = 2;

    public bool WithIdenticalC = false;
    public bool WithSlap = false;

    public AutoBigGameFishing() : base(UIStrings.BigGameFishing, IDs.Actions.BigGameFishing)
    {
    }

    public override string GetName()
        => Name = UIStrings.BigGameFishing;

    public override bool CastCondition()
    {
        if (Service.WorldState.HasStatus(IDs.Status.BigGameFishing))
            return false;

        var slapOrIc = true;
        if (WithIdenticalC || WithSlap)
            slapOrIc = WithIdenticalC && Service.WorldState.HasStatus(IDs.Status.IdenticalCast) ||
                       WithSlap && Service.WorldState.HasStatus(IDs.Status.SurfaceSlap);

        var hasStacks = Service.WorldState.HasAnglersArtStacks(AnglersStacks);

        return hasStacks && slapOrIc;
    }

    protected override DrawOptionsDelegate DrawOptions => () =>
    {
        var stack = AnglersStacks;
        if (DrawUtil.EditNumberField(UIStrings.TabAutoCasts_DrawExtraOptionsThaliaksFavor_, ref stack, "", 1))
        {
            AnglersStacks = Math.Max(2, Math.Min(stack, 10));
            Service.Save();
        }

        DrawUtil.Checkbox(UIStrings.UseIcActive, ref WithIdenticalC);
        DrawUtil.Checkbox(UIStrings.UseSlapActive, ref WithSlap);
    };

    public override int Priority { get; set; } = 18;
    public override bool IsExcludedPriority { get; set; } = false;
}
