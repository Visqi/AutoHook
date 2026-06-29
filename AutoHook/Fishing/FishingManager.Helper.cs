using Dalamud.Game.Chat;
using Dalamud.Game.Text;

namespace AutoHook.Fishing;

public partial class FishingManager {
    private void AnimationCancel() {
        if (GetAutoCastCfg().RecastAnimationCancel)
            PlayerRes.CastAction(IDs.Actions.Collect);

        if (Ws.HasStatus(IDs.Status.Salvage) && GetAutoCastCfg().ChumAnimationCancel)
            PlayerRes.CastAction(IDs.Actions.Salvage);
    }

    private void OnLogMessage(ILogMessage message) {
        var isGenericLure = message.LogMessageId is LogMessageIds.AmbLureSuccess or LogMessageIds.ModLureSuccess;
        var success = GetHookCfg().GetHookset().CastLures.LureTarget switch {
            LureTarget.Any => isGenericLure,
            LureTarget.NotSpecial => isGenericLure,
            _ => false
        };

        if (success)
            Ws.Execute(new FishingInfo.OpSetLureSuccess(true));

        if (message.LogMessageId is LogMessageIds.CantFish)
            Service.Status = UIStrings.CantFishHere;
    }

    private void CheckForSpecialLure(IHandleableChatMessage message) {
        if (message.LogKind is not XivChatType.Gathering) return;
        var isSpecialLure = GameRes.LureFishes.FirstOrDefault(f => f.LureMessage == message.Message.TextValue) != null;
        var success = GetHookCfg().GetHookset().CastLures.LureTarget switch {
            LureTarget.Any => isSpecialLure,
            LureTarget.Special => isSpecialLure,
            _ => false
        };

        if (success)
            Ws.Execute(new FishingInfo.OpSetLureSuccess(true));
    }

    // This is my stupid way of handling the counter for stop/quit fishing and bait/preset swap
    public static class FishingHelper {
        public static Dictionary<Guid, int> FishCount = [];
        public static List<Guid> FishPresetSwapped = [];
        public static List<Guid> FishBaitSwapped = [];

        public static List<Guid> ToBeRemoved = [];

        public static void AddFishCount(Guid guid) {
            FishCount.TryAdd(guid, 0);
            FishCount[guid]++;

            GetFishCount(guid);
        }

        public static void AddBaitSwap(Guid guid) {
            if (!FishBaitSwapped.Contains(guid))
                FishBaitSwapped.Add(guid);
        }

        public static void AddPresetSwap(Guid guid) {
            if (!FishPresetSwapped.Contains(guid))
                FishPresetSwapped.Add(guid);
        }

        public static void RemovePresetSwap(Guid guid) {
            if (SwappedPreset(guid))
                FishPresetSwapped.Remove(guid);
        }

        public static int GetFishCount(Guid guid) {
            return !FishCount.TryGetValue(guid, out var value) ? 0 : value;
        }

        public static bool SwappedBait(Guid guid) {
            return FishBaitSwapped.Any(g => g == guid);
        }

        public static bool SwappedPreset(Guid guid) {
            return FishPresetSwapped.Any(g => g == guid);
        }

        public static void RemoveId(Guid guid) {
            FishCount.Remove(guid);

            if (SwappedPreset(guid))
                FishPresetSwapped.Remove(guid);

            if (SwappedBait(guid))
                FishBaitSwapped.Remove(guid);
        }

        public static void RemoveGuidQueue() {
            foreach (var guid in ToBeRemoved) {
                FishCount.Remove(guid);

                if (SwappedPreset(guid))
                    FishPresetSwapped.Remove(guid);

                if (SwappedBait(guid))
                    FishBaitSwapped.Remove(guid);
            }

            ToBeRemoved.Clear();
        }

        public static void Reset() {
            FishCount = [];
            FishPresetSwapped = [];
            FishBaitSwapped = [];
        }
    }
}
