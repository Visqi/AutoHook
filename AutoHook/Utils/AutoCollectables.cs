using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.Text;
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
        Svc.AddonLifecycle.RegisterListener(AddonEvent.PostUpdate, "SelectYesno", HandleAddon);
    }

    public void Dispose() {
        Svc.AddonLifecycle.UnregisterListener(HandleAddon);
    }

    private readonly List<string> collectablePatterns =
    [
        "collectability of",
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

        if (_pendingResolve) {
            if (TrySelectYesNo(addon, _pendingForceNo))
                _pendingResolve = false;
            return;
        }

        if (!Service.Configuration.AutoCollectablesEnabled)
            return;

        TrySelectYesNo(addon, forceNo: false);
    }

    private unsafe bool TrySelectYesNo(AddonSelectYesno* addon, bool forceNo) {
        var text = addon->PromptText->NodeText.AsReadOnlySeString();
        if (!text.ContainsAny(collectablePatterns))
            return false;

        var name = Enum.GetValues<SeIconChar>().Cast<SeIconChar>().Aggregate(addon->AtkValues[15].String.AsDalamudSeString().GetText(), (current, enumValue) => current.Replace(enumValue.ToIconString(), "")).Trim();
        if (FindRow<Item>(x => x.IsCollectable && !x.Singular.IsEmpty && name.Contains(x.Singular.GetText(), StringComparison.InvariantCultureIgnoreCase)) is not { RowId: > 0 } item)
            return false;

        Svc.Log.Debug($"[AutoCollectables] Detected item [#{item.RowId}] {item.Name}");

        if (forceNo) {
            Svc.Log.Debug($"[AutoCollectables] Force NO for [#{item.RowId}] {item.Name}");
            No(addon);
            return true;
        }

        if (!int.TryParse(Regex.Match(text.ExtractText(), @"\d+").Value, out var value))
            return false;

        if (CollectablesShopItem.FirstOrNull(x => x.Item.Value.RowId == item.RowId) is { } collectability) {
            var min = collectability.CollectablesShopRefine.Value.LowCollectability;
            Svc.Log.Debug($"[AutoCollectables] Minimum collectability required is {min}, value detected is {value}");
            if (value >= min) {
                Svc.Log.Debug($"[AutoCollectables] Entry is [#{item.RowId}] {item.Name} with a sufficient collectability of {value}");
                Yes(addon);
            }
            else {
                Svc.Log.Debug($"[AutoCollectables] Entry is [#{item.RowId}] {item.Name} with an insufficient collectability of {value}");
                No(addon);
            }

            return true;
        }

        if (item.AetherialReduce > 0) {
            Svc.Log.Debug($"[AutoCollectables] Entry is [#{item.RowId}] {item.Name} and probably an aethersand fish. Skipping collectability check.");
            Yes(addon);
            return true;
        }

        if (TryGetRow<WKSItemInfo>(item.AdditionalData.RowId, out var wksItem)) {
            Svc.Log.Debug($"[AutoCollectables] Entry is [#{item.RowId}] {item.Name} for {wksItem.WKSItemSubCategory.ValueNullable?.Name ?? "null"}. Skipping collectability check.");
            Yes(addon);
            return true;
        }

        Svc.Log.Debug($"[AutoCollectables] Failed to find matching CollectablesShopItem for [#{item.RowId}] {item.Name}. Not an aethersand fish or a CE fish.");
        return false;
    }

    public static unsafe void Yes(AddonSelectYesno* addon) {
        var evt = new AtkEvent() { Listener = &addon->AtkUnitBase.AtkEventListener, Target = &AtkStage.Instance()->AtkEventTarget };
        var data = new AtkEventData();
        addon->ReceiveEvent(AtkEventType.ButtonClick, 0, &evt, &data);
    }

    public static unsafe void No(AddonSelectYesno* addon) {
        var evt = new AtkEvent() { Listener = &addon->AtkUnitBase.AtkEventListener, Target = &AtkStage.Instance()->AtkEventTarget };
        var data = new AtkEventData();
        addon->ReceiveEvent(AtkEventType.ButtonClick, 1, &evt, &data);
    }
}
