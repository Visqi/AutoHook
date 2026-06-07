using Dalamud.Game.Chat;
using Dalamud.Game.Text;
using Lumina.Excel.Sheets;

namespace AutoHook.Fishing;

public partial class FishingManager {
    private void AnimationCancel() {
        if (GetAutoCastCfg().RecastAnimationCancel)
            PlayerRes.CastAction(IDs.Actions.Collect);

        if (Ws.HasStatus(IDs.Status.Salvage) && GetAutoCastCfg().ChumAnimationCancel)
            PlayerRes.CastAction(IDs.Actions.Salvage);
    }

    private void OnMessageDelegate(IHandleableChatMessage message) {
        try {
            if (message.LogKind is not XivChatType.Gathering)
                return;

            var text = message.Message.TextValue;
            var logMessage = FindRow<LogMessage>(x => x.Text.ToString() == text);

            var isSpecialFishLure = GameRes.LureFishes.FirstOrDefault(f => f.LureMessage == text) != null;
            var isGenericLureSuccess = logMessage is { RowId: XivChatLog.AmbLureSuccess or XivChatLog.ModLureSuccess };

            var success = GetHookCfg().GetHookset().CastLures.LureTarget switch {
                LureTarget.Any => isSpecialFishLure || isGenericLureSuccess,
                LureTarget.Special => isSpecialFishLure,
                LureTarget.NotSpecial => isGenericLureSuccess,
                _ => false
            };

            if (success)
                Ws.Execute(new FishingInfo.OpSetLureSuccess(true));

            if (logMessage is { RowId: XivChatLog.CantFish })
                Service.Status = UIStrings.CantFishHere;
        }
        catch (Exception e) {
            Svc.Log.Error(e.Message);
        }
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
