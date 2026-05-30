using Dalamud.Bindings.ImGui;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.ClientState.Conditions;
using ECommons.UIHelpers.AddonMasterImplementations;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Event;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using FFXIVClientStructs.Interop;
using Lumina.Excel.Sheets;

namespace AutoHook.Tasks;

public sealed class AetherialReduction(FishingManager fishingManager) : AutoTask, IAutoTaskHooks {
    public void DrawDebug() {
        ImGui.Text($"Reduction unlocked: {IsUnlocked()}");
        ImGui.Text($"Reduceable fish (slots): {CountReduceableFish()}");
        ImGui.Text($"Free inventory slots: {CountFreeInventorySlots()}");
        ImGui.Text($"CanPurifyAny: {CanPurifyAny()}");
        ImGui.Text($"PurifyResult open: {IsPurifyResultOpen()}");
        ImGui.Text($"Blocked for reduction: {IsBlockedForReduction()}");
        ImGui.Text($"FishingState: {Service.WorldState.FishingState}");
        ImGui.Text($"FishingStep: {Service.WorldState.FishingStep}");
        ImGui.Text($"IsCastAvailable: {Service.WorldState.IsCastAvailable()}");
        ImGui.Text($"BlockCasting: {Service.WorldState.BlockCasting}");
    }

    protected override async Task Execute() {
        if (!IsUnlocked()) {
            Service.PrintChat(UIStrings.AetherialReduction_NotUnlocked);
            return;
        }

        if (!CanPurifyAny()) {
            Log("No reduceable fish in inventory");
            return;
        }

        Status = UIStrings.AetherialReduction_Status_Quitting;
        await QuitFishing();

        Status = UIStrings.AetherialReduction_Status_Reducing;
        await ReduceAll();

        await WaitUntilIdleForReduction();

        Status = UIStrings.AetherialReduction_Status_Resuming;
        await ResumeFishing();
        Service.PrintChat(UIStrings.AetherialReduction_Complete);
    }

    public void SetupHooks() { }

    public void EnableHooks()
        => Svc.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "PurifyResult", OnPurifyResultSetup);

    public void DisableHooks()
        => Svc.AddonLifecycle.UnregisterListener(AddonEvent.PostSetup, "PurifyResult", OnPurifyResultSetup);

    public void DisposeHooks() { }

    private async Task QuitFishing() {
        using var scope = BeginScope(nameof(QuitFishing));
        var ws = Service.WorldState;

        if (ws.FishingState == FishingState.None)
            return;

        ws.Execute(new WorldState.OpSetFishingStep(FishingSteps.Quitting));

        await WaitUntil(() => {
            if (ws.FishingState == FishingState.None)
                return true;

            if (ws.ActionAvailable(IDs.Actions.Quit, ActionType.Action) && !ws.Player.BlockCasting)
                PlayerRes.CastActionDelayed(IDs.Actions.Quit, ActionType.Action, "Quit");

            return false;
        }, nameof(QuitFishing), checkFrequency: 5);
    }

    private async Task ReduceAll() {
        using var scope = BeginScope(nameof(ReduceAll));

        while (CanPurifyAny()) {
            await WaitUntilIdleForReduction();

            if (!TryGetNextPurifyableItem(out var item))
                break;

            InventoryType container;
            int slot;
            uint itemId;
            unsafe {
                container = item.Value->Container;
                slot = item.Value->Slot;
                itemId = item.Value->ItemId;
            }

            PurifyItem(item);
            await WaitUntilPurifyCycleComplete(item);
        }
    }

    private async Task WaitUntilPurifyCycleComplete(Pointer<InventoryItem> item) {
        using var scope = BeginScope(nameof(WaitUntilPurifyCycleComplete));

        await WaitUntil(() => IsPurifyResultOpen() || !IsPurifyable(item), "WaitPurifyStart", checkFrequency: 2);

        if (IsPurifyResultOpen())
            await WaitUntil(() => !IsPurifyResultOpen(), "WaitPurifyResultClose", checkFrequency: 2);

        await WaitUntil(() => !IsBlockedForReduction(), "WaitUnoccupiedAfterReduce", checkFrequency: 5);
    }

    private async Task WaitUntilIdleForReduction() {
        using var scope = BeginScope(nameof(WaitUntilIdleForReduction));
        await WaitUntil(() => !IsPurifyResultOpen() && !IsBlockedForReduction(), nameof(WaitUntilIdleForReduction), checkFrequency: 5);
    }

    private async Task ResumeFishing() {
        using var scope = BeginScope(nameof(ResumeFishing));
        var ws = Service.WorldState;

        await WaitUntil(() => ws.FishingState == FishingState.None && ws.IsCastAvailable() && !IsBlockedForReduction() && !IsPurifyResultOpen(), nameof(ResumeFishing), checkFrequency: 5);

        fishingManager.StartFishing();
    }

    private unsafe void OnPurifyResultSetup(AddonEvent type, AddonArgs args) {
        new AddonMaster.PurifyResult(args.GetAddon<AtkUnitBase>()).Close();
    }

    private static bool IsUnlocked()
        => QuestManager.IsQuestComplete(67633);

    private static int CountReduceableFish() {
        var count = 0;
        foreach (var bag in InventoryType.Bags) {
            count += bag.Items.Count(i => i is { ItemKind: Dalamud.Utility.ItemKind.Collectible, GameData.ValueNullable.AetherialReduce: > 0 });
        }

        return count;
    }

    private static int CountFreeInventorySlots() {
        var count = 0;
        foreach (var bag in InventoryType.Bags) {
            count += bag.Items.Count(i => i.IsValid);
        }

        return count;
    }

    private static bool CanPurifyAny()
        => TryGetNextPurifyableItem(out _);

    private static unsafe bool IsPurifyResultOpen()
        => TryGetAddonByName<AtkUnitBase>("PurifyResult", out var addon) && addon->IsVisible;

    private static bool IsBlockedForReduction()
        => IsOccupied() || Svc.Condition[ConditionFlag.Occupied39];

    private static bool TryGetNextPurifyableItem(out Pointer<InventoryItem> item) {
        item = default;
        foreach (var bag in InventoryType.Bags) {
            if (bag.Items.FirstOrDefault(i => i is { ItemKind: Dalamud.Utility.ItemKind.Collectible, GameData.ValueNullable.AetherialReduce: > 0 }) is { } i) {
                item = i;
                return true;
            }
        }

        return false;
    }

    private static unsafe bool IsPurifyable(Pointer<InventoryItem> item)
        => item.Value->ItemId != 0 && item.Value->Flags == InventoryItem.ItemFlags.Collectable && Item.GetRow(item.Value->ItemId) is { AetherialReduce: > 0 };

    private static unsafe void PurifyItem(Pointer<InventoryItem> item) {
        var agent = AgentPurify.Instance();
        if (agent == null) {
            Svc.Log.Debug("[AetherialReduction] AgentPurify is null");
            return;
        }

        agent->ReduceItem(item);
        Svc.Log.Debug($"[AetherialReduction] Reducing [{item.Value->ItemId}] {item.Value->Container}/{item.Value->Slot}");
    }
}
