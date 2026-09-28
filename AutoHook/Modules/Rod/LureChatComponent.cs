using Dalamud.Game.Chat;
using Dalamud.Game.Text;

namespace AutoHook.Modules.Rod;

public sealed class LureChatComponent(RodFishingModule module) : RodComponent(module) {
    public void AnimationCancel() {
        if (Rod.GetAutoCastCfg().RecastAnimationCancel)
            Rod.ExecuteImmediate(new ActionRequest(IDs.Actions.Collect, UseRaw: true));

        if (Ws.Player.HasStatus(IDs.Status.Salvage) && Rod.GetAutoCastCfg().ChumAnimationCancel)
            Rod.ExecuteImmediate(new ActionRequest(IDs.Actions.Salvage, UseRaw: true));
    }

    public void OnLogMessage(ILogMessage message) {
        var isGenericLure = message.LogMessageId is LogMessageIds.AmbLureSuccess or LogMessageIds.ModLureSuccess;
        var active = Rod.GetHookCfg().GetHookset().CastLures.GetActiveOption(Ws);
        if (active != null && AutoLures.MatchesLureSuccess(active.Value.Target, isGenericLure, isSpecialLure: false))
            Ws.Execute(new FishingInfo.OpSetLureSuccess(true));

        if (message.LogMessageId is LogMessageIds.CantFish)
            Service.Status = UIStrings.CantFishHere;
    }

    public void CheckForSpecialLure(IHandleableChatMessage message) {
        if (message.LogKind is not XivChatType.Gathering) return;
        var isSpecialLure = GameRes.LureFishes.FirstOrDefault(f => f.LureMessage == message.Message.TextValue) != null;
        var active = Rod.GetHookCfg().GetHookset().CastLures.GetActiveOption(Ws);
        if (active != null && AutoLures.MatchesLureSuccess(active.Value.Target, isGenericLure: false, isSpecialLure))
            Ws.Execute(new FishingInfo.OpSetLureSuccess(true));
    }
}
