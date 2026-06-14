using clib.TaskSystem;
using System.Numerics;

namespace AutoHook.Tasks;

public sealed class AutoOceanFish(FishingManager fishingManager, uint zoneIndex) : TaskBase {
    public uint ZoneIndex { get; } = zoneIndex;
    private static readonly Random Rng = new();

    protected override async Task Execute() {
        Service.PrintDebug($"[AutoOceanFish] Task execute zone={ZoneIndex + 1}, walkToRailing={ZoneIndex == 0}");

        if (ZoneIndex == 0) {
            Status = "Walking to railing";
            Service.PrintDebug("[AutoOceanFish] Walking to railing");
            await WalkToRailing();
        }

        Status = "Starting fishing";
        var ws = Service.WorldState;
        await WaitUntil(() => (Svc.Objects.LocalPlayer?.IsTargetable ?? false) && ws.IsCastAvailable(), nameof(Execute), checkFrequency: 5);
        Service.PrintDebug("[AutoOceanFish] Calling StartFishing");
        fishingManager.StartFishing();
        Service.PrintDebug("[AutoOceanFish] StartFishing returned");
    }

    // https://github.com/Knightmore/Henchman/blob/4aa8cf33b6164536acca81afefa0df5da6740e89/Henchman/Features/OnABoat/OnABoat.cs#L120
    internal static Vector3 GetFishingPosition() {
        var left = new Vector3(7 + Rng.NextSingle() * 0.25f, 6.711f, Rng.Next(2) == 0 ? Rng.NextSingle() * 10f + -14f : Rng.NextSingle() * 7f + -2f);
        var right = new Vector3(-7 - Rng.NextSingle() * 0.25f, 6.711f, Rng.NextSingle() * 15.5f + -10f);
        return Rng.Next(2) == 0 ? left : right;
    }

    private async Task WalkToRailing() {
        var position = GetFishingPosition();
        var rotation = position.X > 0 ? 1.5f : -1.5f;
        await MoveToDirectly(position, () => Player.Object.WithinRange(position, 1) && Service.WorldState.IsCastAvailable());
        await NextFrame(500);
        unsafe {
            Svc.Objects.LocalPlayer?.Character->SetRotation(rotation);
        }
    }
}
