using AutoHook.Conditions;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.Sheets;
using System.Text.RegularExpressions;

namespace AutoHook.Utils;

public class AutoCollectables : IDisposable {
    private bool _pendingResolve;
    private bool _pendingForceNo;

    public AutoCollectables() {
        Svc.AddonLifecycle.RegisterListener(AddonEvent.PostUpdate, "SelectYesno", HandleAddon); // onupdate instead of setup since the pending can trigger before setup fires
    }

    public void Dispose() {
        Svc.AddonLifecycle.UnregisterListener(HandleAddon);
    }

    // TODO: handle new line characters in the string. Large ui scale changes the actual string in the addon
    private readonly List<string> collectablePatterns =
    [
        "Preserve the following",
        "収集価値",
        "Sammlerwert",
        "Valeur de collection"
        // if someone could add the chinese and korean translations that'd be nice
    ];

    public void RequestResolve(bool forceNo) {
        _pendingForceNo = forceNo;
        _pendingResolve = true;
    }

    private unsafe void HandleAddon(AddonEvent type, AddonArgs args) {
        if (!Service.Configuration.PluginEnabled)
            return;

        var addon = args.GetAddon<AddonSelectYesno>();
        if (!addon->AtkUnitBase.IsReady)
            return;

        if (IsWaitingOnConditions()) {
            _pendingResolve = false;
            return;
        }

        if (_pendingResolve) {
            if (TrySelectYesNo(addon, _pendingForceNo))
                _pendingResolve = false;
            return;
        }

        if (!Service.Configuration.AutoCollectablesEnabled)
            return;

        TrySelectYesNo(addon, forceNo: false);
    }

    private bool IsWaitingOnConditions() {
        var p = Service.Configuration.HookPresets;
        var sets = (p.SelectedPreset?.ExtraCfg.Enabled ?? false ? p.SelectedPreset!.ExtraCfg : p.DefaultPreset.ExtraCfg).Triggers
            .Where(t => t is { Enabled: true, ResolveCollectablesWindow: true, ConditionSet: not null } && t.ConditionSet.HasAnyCondition())
            .Select(t => t.ConditionSet!);

        return sets.Any() && sets.All(set => !set.Evaluate(Service.WorldState, ConditionRegistry.Registry));
    }

    private unsafe bool TrySelectYesNo(AddonSelectYesno* addon, bool forceNo) {
        var text = addon->PromptText->NodeText.AsReadOnlySeString();
        if (!text.ContainsAny(collectablePatterns))
            return false;

        if (Item.GetRow(ItemUtil.GetBaseId(addon->AtkValues[14].UInt).ItemId) is not { IsCollectable: true, RowId: > 0 } item)
            return false;

        if (forceNo) {
            Answer(addon, false);
            return true;
        }

        if (!int.TryParse(Regex.Match(text.ExtractText(), @"\d+").Value, out var value))
            return false;

        if (CollectablesShopItem.FirstOrNull(x => x.Item.Value.RowId == item.RowId) is { } collectability) {
            if (value >= collectability.CollectablesShopRefine.Value.LowCollectability)
                Answer(addon, true);
            else
                Answer(addon, false);

            return true;
        }

        if (item.AetherialReduce > 0) {
            Answer(addon, true);
            return true;
        }

        if (TryGetRow<WKSItemInfo>(item.AdditionalData.RowId, out _)) {
            Answer(addon, true);
            return true;
        }

        return false;
    }

    public static unsafe void Answer(AddonSelectYesno* addon, bool IsYes) {
        var evt = new AtkEvent() { Listener = &addon->AtkUnitBase.AtkEventListener, Target = &AtkStage.Instance()->AtkEventTarget };
        var data = new AtkEventData();
        addon->ReceiveEvent(AtkEventType.ButtonClick, IsYes ? 0 : 1, &evt, &data);
    }
}
