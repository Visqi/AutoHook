using FFXIVClientStructs.FFXIV.Client.Game.InstanceContent;

namespace AutoHook.Data;

public readonly record struct OceanMission(uint Type, ushort Progress) {
    public override string ToString() => $"Type={Type}, Progress={Progress}";
}

public enum TimeOfDay {
    None = 0,
    Day = 1,
    Sunset = 2,
    Night = 3
}

public sealed class OceanFishingState {
    public static readonly OceanFishingState Empty = new();

    public bool SpectralCurrentActive { get; init; }
    public uint CurrentRoute { get; init; }
    public TimeOfDay TimeOfDay { get; init; }
    public uint CurrentZone { get; init; } // IKDSpot
    public uint CurrentSpotId { get; init; } // IKDTimeDefine
    public uint CurrentTimeId { get; init; }
    public float TimeLeftInZone { get; init; } // seconds
    public float ZoneTimeMax { get; init; } // seconds

    public OceanMission Mission1 { get; init; }
    public OceanMission Mission2 { get; init; }
    public OceanMission Mission3 { get; init; }

    public IReadOnlyList<InstanceContentOceanFishing.FishDataStruct> FishData { get; init; } = [];
    public InstanceContentOceanFishing.OceanFishingStatus Status { get; init; }
}

public sealed class OceanFishInfo {
    public const float SpectralDefaultDurationSeconds = 120f;
    private const float SpectralMaxDurationSeconds = 180f;
    private const float SpectralZoneEndBufferSeconds = 30f;
    // Spectral: next/remaining duration, carry-over from cut short, skip tracking across zones
    private uint _lastRoute;
    private uint _lastZone = uint.MaxValue;
    private bool _spectralWasActive;
    private bool _previousZoneHadSpectral;
    private float _spectralSeconds = SpectralDefaultDurationSeconds;
    private float _spectralCarrySeconds;
    private DateTime _spectralLastUtc = DateTime.MinValue;
    private readonly List<ZoneSpectralRecord> _spectralHistory = [];

    private InstanceContentOceanFishing.OceanFishingStatus _lastOceanStatus;

    public OceanFishingState OceanFishing { get; set; } = OceanFishingState.Empty;
    public SpectralCurrentStatus SpectralCurrentStatus { get; set; }
    public OceanSpectralTimerInfo SpectralTimer { get; private set; } = OceanSpectralTimerInfo.Inactive;

    public IReadOnlyList<ZoneSpectralRecord> SpectralHistory => _spectralHistory;

    public IEnumerable<WorldState.Operation> CompareToInitial() {
        if (OceanFishing != OceanFishingState.Empty)
            yield return new OpOceanFishing(OceanFishing);
        if (SpectralTimer.IsActive || SpectralTimer.NextSpectralDuration != SpectralDefaultDurationSeconds)
            yield return new OpSpectralTimer(SpectralTimer);
    }

    private void TickSpectralTimer(OceanFishingState state, float zoneTimeLeft) {
        var now = DateTime.UtcNow;
        var delta = _spectralLastUtc == DateTime.MinValue ? 0f : (float)(now - _spectralLastUtc).TotalSeconds;
        _spectralLastUtc = now;

        if (state.CurrentRoute != _lastRoute) {
            ResetSpectral();
            _lastRoute = state.CurrentRoute;
        }

        if (state.CurrentZone != _lastZone) {
            if (!_previousZoneHadSpectral && state.CurrentZone != 0)
                _spectralSeconds = SpectralMaxDurationSeconds;
            _previousZoneHadSpectral = false;
            _lastZone = state.CurrentZone;
        }

        var active = state.SpectralCurrentActive;
        if (active != _spectralWasActive) {
            if (active)
                BeginSpectral(zoneTimeLeft);
            else
                EndSpectral();
            _spectralWasActive = active;
        }
        else if (active && delta > 0f) {
            _spectralSeconds = Math.Max(0f, _spectralSeconds - delta);
        }

        var remaining = active ? _spectralSeconds : 0f;
        var next = active ? _spectralSeconds : Math.Min(SpectralDefaultDurationSeconds + _spectralCarrySeconds, SpectralMaxDurationSeconds);
        SpectralTimer = new OceanSpectralTimerInfo(remaining, active, next);
    }

    private IEnumerable<WorldState.Operation> TickZoneStarted(OceanFishingState state) {
        if (_lastOceanStatus != InstanceContentOceanFishing.OceanFishingStatus.Fishing && state.Status == InstanceContentOceanFishing.OceanFishingStatus.Fishing) {
            Service.PrintDebug($"[OceanZone] ZoneStarted ({_lastOceanStatus} -> {state.Status}), {OceanStopUtil.FormatStateLog(state)}");
            yield return new WorldState.OpOceanZoneStarted(state.CurrentZone);
        }

        _lastOceanStatus = state.Status;
    }

    private void ResetSpectral() {
        _lastRoute = 0;
        _lastZone = uint.MaxValue;
        _spectralWasActive = false;
        _previousZoneHadSpectral = false;
        _spectralSeconds = SpectralDefaultDurationSeconds;
        _spectralCarrySeconds = 0f;
        _spectralLastUtc = DateTime.MinValue;
        _spectralHistory.Clear();
    }

    private void ResetZoneLifecycle() => _lastOceanStatus = default;

    private void ResetOceanTrackers() {
        ResetSpectral();
        ResetZoneLifecycle();
        SpectralTimer = OceanSpectralTimerInfo.Inactive;
    }

    private void BeginSpectral(float zoneTimeLeft) {
        var cap = zoneTimeLeft - SpectralZoneEndBufferSeconds;
        if (cap < _spectralSeconds) {
            _spectralCarrySeconds = _spectralSeconds - cap;
            _spectralSeconds = Math.Max(0f, cap);
        }
        else {
            _spectralCarrySeconds = 0f;
        }

        _previousZoneHadSpectral = true;
        _spectralHistory.Add(new ZoneSpectralRecord(
            (int)_lastZone,
            DateTime.UtcNow,
            null,
            _spectralSeconds + _spectralCarrySeconds,
            _spectralCarrySeconds));
    }

    private void EndSpectral() {
        if (_spectralHistory.Count > 0) {
            var last = _spectralHistory[^1];
            _spectralHistory[^1] = last with { Ended = DateTime.UtcNow };
        }

        _spectralSeconds = Math.Min(SpectralDefaultDurationSeconds + _spectralCarrySeconds, SpectralMaxDurationSeconds);
    }

    public sealed record OpOceanFishing(OceanFishingState? State) : WorldState.Operation {
        protected override void Exec(WorldState ws) {
            var s = State ?? OceanFishingState.Empty;
            var inOcean = State != null;
            ws.Ocean.OceanFishing = s;
            ws.Ocean.SpectralCurrentStatus = s.SpectralCurrentActive ? SpectralCurrentStatus.Active : SpectralCurrentStatus.NotActive;
            if (!inOcean) {
                ws.Ocean.ResetOceanTrackers();
                return;
            }

            ws.Ocean.TickSpectralTimer(s, s.TimeLeftInZone);
            foreach (var op in ws.Ocean.TickZoneStarted(s))
                op.Execute(ws);
        }
    }

    public sealed record OpSpectralTimer(OceanSpectralTimerInfo Value) : WorldState.Operation {
        protected override void Exec(WorldState ws) => ws.Ocean.SpectralTimer = Value;
    }
}

public readonly record struct ZoneSpectralRecord(
    int ZoneIndex,
    DateTime Started,
    DateTime? Ended,
    float PlannedDurationSeconds,
    float CarriedExtraSeconds) {
    public float? ActualDurationSeconds
        => Ended is { } end ? (float)(end - Started).TotalSeconds : null;
}

public readonly record struct OceanSpectralTimerInfo(float TimeRemaining, bool IsActive, float NextSpectralDuration) {
    public static OceanSpectralTimerInfo Inactive { get; } = new(0f, false, OceanFishInfo.SpectralDefaultDurationSeconds);
}
