using AutoHook.Conditions;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.Text;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.UI;
using Lumina.Excel.Sheets;
using System.Text.RegularExpressions;

namespace AutoHook.Utils;

public class AutoCollectables : IDisposable {
    public AutoCollectables() {
        Svc.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "SelectYesno", HandleAddon);
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

    // public method for handling the addon outside of postsetup
    public unsafe void ResolvePending(bool forceNo) {
        var mgr = RaptureAtkUnitManager.Instance();
        if (mgr == null)
            return;

        var unit = mgr->GetAddonByName("SelectYesno");
        if (unit == null || !unit->IsReady)
            return;

        var addon = (AddonSelectYesno*)unit;
        SelectYesNo(addon, forceNo);
    }

    private unsafe void HandleAddon(AddonEvent type, AddonArgs args) {
        if (!Service.Configuration.PluginEnabled || !Service.Configuration.AutoCollectablesEnabled)
            return;

        var addon = args.GetAddon<AddonSelectYesno>();
        SelectYesNo(addon, forceNo: false);
    }

    private unsafe void SelectYesNo(AddonSelectYesno* addon, bool forceNo) {
        var text = addon->PromptText->NodeText.AsReadOnlySeString();
        if (!text.ContainsAny(collectablePatterns))
            return;

        var name = Enum.GetValues<SeIconChar>().Cast<SeIconChar>().Aggregate(addon->AtkValues[15].String.AsDalamudSeString().GetText(), (current, enumValue) => current.Replace(enumValue.ToIconString(), "")).Trim();
        if (FindRow<Item>(x => x.IsCollectable && !x.Singular.IsEmpty && name.Contains(x.Singular.GetText(), StringComparison.InvariantCultureIgnoreCase)) is not { RowId: > 0 } item)
            return;

        Svc.Log.Debug($"[AutoCollectables] Detected item [{item}] {item.Name}");

        if (forceNo) {
            Svc.Log.Debug($"[AutoCollectables] Force NO for [{item.RowId}] {item.Name}");
            AddonSelectYesno.No();
            return;
        }

        if (!int.TryParse(Regex.Match(text.ExtractText(), @"\d+").Value, out var value))
            return;

        if (CollectablesShopItem.FirstOrNull(x => x.Item.Value.RowId == item.RowId) is { } collectability) {
            var min = collectability.CollectablesShopRefine.Value.LowCollectability;
            Svc.Log.Debug($"[AutoCollectables] Minimum collectability required is {min}, value detected is {value}");
            if (value >= min) {
                Svc.Log.Debug($"[AutoCollectables] Entry is [{item}] {item.Name} with a sufficient collectability of {value}");
                AddonSelectYesno.Yes();
            }
            else {
                Svc.Log.Debug($"[AutoCollectables] Entry is [{item}] {item.Name} with an insufficient collectability of {value}");
                AddonSelectYesno.No();
            }
        }
        else {
            if (item.AetherialReduce > 0) {
                Svc.Log.Debug($"[AutoCollectables] Entry is [#{item.RowId}] {item.Name} and probably an aethersand fish. Skipping collectability check.");
                AddonSelectYesno.Yes();
            }
            else if (TryGetRow<WKSItemInfo>(item.AdditionalData.RowId, out var wksItem)) {
                Svc.Log.Debug($"[AutoCollectables] Entry is [#{item.RowId}] {item.Name} for {wksItem.WKSItemSubCategory.ValueNullable?.Name ?? "null"}. Skipping collectability check.");
                AddonSelectYesno.Yes();
            }
            else {
                Svc.Log.Debug($"[AutoCollectables] Failed to find matching CollectablesShopItem for [{item.RowId}] {item.Name}. Not an aethersand fish or a CE fish.");
            }
        }
    }
}
