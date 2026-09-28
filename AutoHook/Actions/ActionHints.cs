namespace AutoHook.Actions;

public enum HintSource {
    BiteHook,
    FishCaught,
    AutoCast,
    Spectral,
    Timeout,
    Extra,
    Lure,
    Gig,
    GigAutoCast,
}

public sealed class CastHint {
    public int Priority { get; init; }
    public ActionRequest Request { get; init; }
    public HintSource Source { get; init; }
    public string Detail { get; init; } = "";
    public DecisionContext Context { get; init; }
    public List<ActionRequest>? Chain { get; init; }
    public Action? AfterExecute { get; init; }
}

public abstract record SideEffectHint(HintSource Source, string Detail);

public sealed record SwapPresetHint(string PresetName, HintSource Source = HintSource.Extra, string Detail = "")
    : SideEffectHint(Source, Detail);

public sealed record SwapBaitHint(BaitFishClass Bait, HintSource Source = HintSource.Extra, string Detail = "")
    : SideEffectHint(Source, Detail);

public sealed record StopFishingHint(ExtraStopAction Action, HintSource Source = HintSource.Extra, string Detail = "")
    : SideEffectHint(Source, Detail);

public sealed record ResetCounterHint(HintSource Source = HintSource.Extra, string Detail = "")
    : SideEffectHint(Source, Detail);

public sealed record RemoveStatusHint(uint StatusId, HintSource Source = HintSource.Extra, string Detail = "")
    : SideEffectHint(Source, Detail);

public sealed record StartFishingHint(HintSource Source = HintSource.Extra, string Detail = "")
    : SideEffectHint(Source, Detail);

public sealed record StartReductionHint(HintSource Source = HintSource.Extra, string Detail = "")
    : SideEffectHint(Source, Detail);

public sealed record NotifyHint(NotificationConfig Config, string FallbackText, HintSource Source = HintSource.Extra, string Detail = "")
    : SideEffectHint(Source, Detail);

public sealed class ActionHints {
    public List<CastHint> Casts { get; } = [];
    public List<SideEffectHint> SideEffects { get; } = [];
    public bool ForbidCast;
    public bool PreferRest;
    public bool StopPlugin;
    public bool HoldForPendingGp;
    public DecisionContext PreferRestContext = DecisionContext.Hook;
    public string PreferRestDetail = "";
    public int PreferRestDelayMs;

    public void Clear() {
        Casts.Clear();
        SideEffects.Clear();
        ForbidCast = false;
        PreferRest = false;
        StopPlugin = false;
        HoldForPendingGp = false;
        PreferRestContext = DecisionContext.Hook;
        PreferRestDetail = "";
        PreferRestDelayMs = 0;
    }

    public void AddCast(
        int priority,
        ActionRequest request,
        HintSource source,
        string? detail = null,
        DecisionContext context = DecisionContext.AutoCast,
        IReadOnlyList<ActionRequest>? chain = null,
        Action? afterExecute = null) {
        Casts.Add(new CastHint {
            Priority = priority,
            Request = request,
            Source = source,
            Detail = detail ?? "",
            Context = context,
            Chain = chain is null ? null : [.. chain],
            AfterExecute = afterExecute,
        });
    }

    public void AddSideEffect(SideEffectHint hint) => SideEffects.Add(hint);

    public bool HasCastProposal => Casts.Count > 0 || PreferRest;
}
