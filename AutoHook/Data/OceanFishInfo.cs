using FFXIVClientStructs.FFXIV.Client.Game.InstanceContent;

namespace AutoHook.Data;

public readonly record struct OceanMission(uint Type, ushort Progress) {
    public override string ToString() => $"Type={Type}, Progress={Progress}";
}

public sealed class OceanFishingState {
    public static readonly OceanFishingState Empty = new();

    public bool SpectralCurrentActive { get; init; }
    public uint CurrentRoute { get; init; }
    public uint CurrentZone { get; init; }

    public OceanMission Mission1 { get; init; }
    public OceanMission Mission2 { get; init; }
    public OceanMission Mission3 { get; init; }

    public IReadOnlyList<InstanceContentOceanFishing.FishDataStruct> FishData { get; init; } = [];
}

public sealed class OceanFishInfo {
    public OceanFishingState OceanFishing { get; set; } = OceanFishingState.Empty;
    public SpectralCurrentStatus SpectralCurrentStatus { get; set; }

    public IEnumerable<WorldState.Operation> CompareToInitial() {
        if (OceanFishing != OceanFishingState.Empty)
            yield return new OpOceanFishing(OceanFishing);
    }

    public sealed record OpOceanFishing(OceanFishingState? State) : WorldState.Operation {
        protected override void Exec(WorldState ws) {
            var s = State ?? OceanFishingState.Empty;
            ws.Ocean.OceanFishing = s;
            ws.Ocean.SpectralCurrentStatus = s.SpectralCurrentActive ? SpectralCurrentStatus.Active : SpectralCurrentStatus.NotActive;
        }
    }
}

