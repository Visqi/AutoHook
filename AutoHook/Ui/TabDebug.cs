using System.Reflection;
using AutoHook.Tasks;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility.Raii;
using ECommons.Throttlers;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace AutoHook.Ui;

public class TabDebug : BaseTab {
    public override OpenWindow Type => OpenWindow.Debug;

    public override string TabName => "Debug";
    public override bool Enabled => true;

    public override void DrawHeader() {
        DrawUtil.TextV("WorldState viewer and wiki presets.");
    }

    public override void Draw() {
        try {
            DrawAutomationTask();
            DrawNotificationMaster();
            DrawWorldState();
            DrawWikiPresets();
        }
        catch (Exception e) {
            Svc.Log.Error(e.Message);
        }
    }

    private static void DrawAutomationTask() {
        if (!ImGui.CollapsingHeader("Aetherial reduction task", ImGuiTreeNodeFlags.DefaultOpen))
            return;

        using (ImRaii.PushIndent()) {
            var automation = Svc.Automation;
            ImGui.Text($"Automation running: {automation.Running}");
            ImGui.Text($"Task name: {automation.Name}");
            ImGui.Text($"Task status: {automation.Status}");

            using (ImRaii.Disabled(!automation.Running)) {
                if (ImGui.Button("Stop current task"))
                    automation.Stop();
            }

            ImGui.Spacing();
            if (Svc.Automation.CurrentTask is AetherialReduction reductionTask)
                reductionTask.DrawDebug();
        }
    }

    private static void DrawWorldState() {
        var ws = Service.WorldState;
        if (ws == null) return;

        if (!ImGui.CollapsingHeader("WorldState", ImGuiTreeNodeFlags.DefaultOpen))
            return;

        using (ImRaii.PushIndent()) {
            // Core
            if (ImGui.CollapsingHeader("Core", ImGuiTreeNodeFlags.DefaultOpen)) {
                ImGui.Text($"CurrentGp / MaxGp: {ws.CurrentGp} / {ws.MaxGp}");
                ImGui.Text($"BlockCasting: {ws.BlockCasting}");
                ImGui.Text($"CurrentWeatherId: {ws.CurrentWeatherId}");
                ImGui.Text($"TerritoryId: {ws.TerritoryId}");
            }

            // Fishing state
            if (ImGui.CollapsingHeader("Fishing state", ImGuiTreeNodeFlags.DefaultOpen)) {
                var f = ws.Fishing;
                ImGui.Text($"FishingState: {ws.FishingState}");
                ImGui.Text($"PreviousFishingState: {ws.PreviousFishingState}");
                ImGui.Text($"FishingStep: {ws.FishingStep} (0x{(uint)ws.FishingStep:X})");
                ImGui.Text($"BaitInfo: BaitId={f.BaitInfo.BaitId} SwimbaitId={f.BaitInfo.SelectedSwimbaitId} MoochId={f.BaitInfo.MoochId} IsMooching={f.BaitInfo.IsMooching}");
            }

            // Bite / context
            if (ImGui.CollapsingHeader("Bite / context")) {
                ImGui.Text($"BiteInfo: Time={ws.Fishing.BiteInfo.BiteTimeSeconds:F2} TugType={ws.Fishing.BiteInfo.TugType}");
                ImGui.Text($"ChumActive: {ws.ChumActive}");
            }

            // Intuition
            if (ImGui.CollapsingHeader("Intuition")) {
                ImGui.Text($"Intuition: Status={ws.Fishing.Intuition.Status} TimeRemaining={ws.Fishing.Intuition.TimeRemaining:F1}s");
                ImGui.Text($"SpectralCurrentStatus: {ws.SpectralCurrentStatus}");
            }

            // Ocean fishing
            if (ImGui.CollapsingHeader("Ocean fishing")) {
                var of = ws.OceanFishing;
                var st = ws.SpectralTimer;
                ImGui.Text($"SpectralCurrentActive: {of.SpectralCurrentActive}");
                ImGui.Text($"SpectralTimeRemaining: {st.TimeRemaining:F1}s (active={st.IsActive})");
                ImGui.Text($"NextSpectralDuration: {st.NextSpectralDuration:F1}s");
                ImGui.Text($"TimeLeftInZone: {of.TimeLeftInZone:F1}s");
                ImGui.Text($"AutoOceanFish: {Service.Configuration.AutoOceanFish}");
                if (Svc.Automation.CurrentTask is AutoOceanFish oceanTask)
                    ImGui.Text($"OceanAutoFishTask: zone {oceanTask.ZoneIndex + 1}, status={oceanTask.Status}");
                ImGui.Text($"CurrentRoute: {of.CurrentRoute}");
                ImGui.Text($"CurrentZone: {of.CurrentZone}");
                ImGui.Text($"Mission1: type={of.Mission1.Type} progress={of.Mission1.Progress}");
                ImGui.Text($"Mission2: type={of.Mission2.Type} progress={of.Mission2.Progress}");
                ImGui.Text($"Mission3: type={of.Mission3.Type} progress={of.Mission3.Progress}");
                ImGui.Text($"FishData count: {of.FishData?.Count ?? 0}");
                if (ws.SpectralHistory.Count > 0) {
                    ImGui.Separator();
                    ImGui.Text("Spectral history (this voyage):");
                    foreach (var rec in ws.SpectralHistory) {
                        var dur = rec.ActualDurationSeconds is { } d ? $"{d:F0}s" : "active";
                        ImGui.BulletText($"zone {rec.ZoneIndex + 1}: planned {rec.PlannedDurationSeconds:F0}s, carried {rec.CarriedExtraSeconds:F0}s, {dur}");
                    }
                }
            }

            // Last catch
            if (ImGui.CollapsingHeader("Last catch")) {
                var lc = ws.Fishing.LastCatch;
                if (lc.HasValue)
                    ImGui.Text($"LastCatch: FishId={lc.Value.FishId} Amount={lc.Value.Amount}");
                else
                    ImGui.Text("LastCatch: (none)");
                if (Service.LastCatch != null)
                    ImGui.Text($"LastCatch (name): {Service.LastCatch.Name} (Id: {Service.LastCatch.Id})");
                ImGui.Text($"SessionCatches count: {ws.Fishing.SessionCatches.Count}");
            }

            // Actions
            if (ImGui.CollapsingHeader("Actions")) {
                var ua = ws.Fishing.LastUsedAction;
                if (ua.HasValue)
                    ImGui.Text($"LastUsedAction: {ua.Value.ActionId} ({ua.Value.ActionType})");
                else
                    ImGui.Text("LastUsedAction: (none)");
                ImGui.Text($"LureSuccess: {ws.LureSuccess}");
                if (ws.Fishing.LastLureCastBiteTime is { } lureCastBiteTime) {
                    var elapsed = ws.Fishing.BiteInfo.BiteTimeSeconds - lureCastBiteTime;
                    ImGui.Text($"LastLureCastBiteTime: {lureCastBiteTime:F2}s (elapsed {elapsed:F2}s)");
                }
                else
                    ImGui.Text("LastLureCastBiteTime: (none)");
            }

            // Statuses
            if (ImGui.CollapsingHeader("Statuses")) {
                foreach (var (id, (time, stacks)) in ws.Statuses) {
                    var name = StatusName(id);
                    ImGui.Text($"{id}: {name} — time={time:F1}s stacks={stacks}");
                }
                if (ws.Statuses.Count == 0)
                    ImGui.TextDisabled("(none)");
            }

            // Swimbait
            if (ImGui.CollapsingHeader("Swimbait")) {
                ImGui.Text($"SwimbaitIds: [{string.Join(", ", ws.SwimbaitIds)}]");
                ImGui.Text($"GetSwimbaitCount(): {ws.GetSwimbaitCount()}");
                ImGui.Text($"IsSwimbaitFull: {ws.IsSwimbaitFull()}");
                ImGui.Text($"IsSwimbaitEmpty: {ws.IsSwimbaitEmpty()}");
            }

            // Pot
            if (ImGui.CollapsingHeader("Pot")) {
                ImGui.Text($"IsPotOffCooldown: {ws.Player.IsPotOffCooldown}");
            }

            // Item counts (known IDs)
            if (ImGui.CollapsingHeader("Item counts (known)")) {
                foreach (var (id, label) in KnownItemIds) {
                    var c = ws.GetItemCount(id);
                    if (c > 0)
                        ImGui.Text($"{label} ({id}): {c}");
                }
            }

            // Action availability (key actions)
            if (ImGui.CollapsingHeader("Action availability (key)")) {
                ImGui.Text($"IsCastAvailable: {ws.IsCastAvailable()}");
                ImGui.Text($"IsMoochAvailable: {ws.IsMoochAvailable()}");
                ImGui.Text($"HasMultihookAvailable: {ws.HasMultihookAvailable()}");
                foreach (var (id, type, label) in KnownActionIds) {
                    if (ws.ActionAvailable(id, type))
                        ImGui.Text($"{label}: available");
                }
            }

            DrawFshActionInfo(ws);
        }
    }

    private static void DrawFshActionInfo(WorldState ws) {
        if (!ImGui.CollapsingHeader("Action info (FSH)"))
            return;

        using (ImRaii.PushIndent()) {
            ImGui.TextDisabled("ActionStatus != 0 blocks use; cooldown from GetRecastGroupDetail.");
            if (!ImGui.BeginTable("fsh_action_info", 8, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY, new System.Numerics.Vector2(0, 280.Scaled()))) {
                ImGui.EndTable();
                return;
            }

            ImGui.TableSetupColumn("Action");
            ImGui.TableSetupColumn("Id");
            ImGui.TableSetupColumn("Type");
            ImGui.TableSetupColumn("Status");
            ImGui.TableSetupColumn("CD (s)");
            ImGui.TableSetupColumn("Grp");
            ImGui.TableSetupColumn("OnCD");
            ImGui.TableSetupColumn("Avail");
            ImGui.TableHeadersRow();

            foreach (var (field, id, type) in FshActions) {
                uint status;
                float cd;
                int group;
                bool onCd;
                bool avail;
                try {
                    status = PlayerRes.ActionStatus(id, type);
                    cd = PlayerRes.GetCooldown(id, type);
                    group = PlayerRes.GetRecastGroups(id, type);
                    onCd = PlayerRes.ActionOnCoolDown(id, type);
                    avail = ws.ActionAvailable(id, type);
                }
                catch (Exception e) {
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    ImGui.Text($"{field} ({id})");
                    ImGui.TableNextColumn();
                    ImGui.TextColored(ImGuiColors.DalamudRed, e.Message);
                    continue;
                }

                var name = MultiString.GetActionName(id);
                var label = string.IsNullOrEmpty(name) ? field : $"{field} ({name})";

                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.TextUnformatted(label);
                ImGui.TableNextColumn();
                ImGui.Text($"{id}");
                ImGui.TableNextColumn();
                ImGui.TextUnformatted(type.ToString());
                ImGui.TableNextColumn();
                ImGui.TextColored(status == 0 ? ImGuiColors.DalamudGrey : ImGuiColors.ParsedOrange, $"{status}");
                ImGui.TableNextColumn();
                ImGui.Text(cd > 0 ? $"{cd:F1}" : "-");
                ImGui.TableNextColumn();
                ImGui.Text(group >= 0 ? $"{group}" : "-");
                ImGui.TableNextColumn();
                ImGui.Text(onCd ? "yes" : "no");
                ImGui.TableNextColumn();
                ImGui.Text(avail ? "yes" : "no");
            }

            ImGui.EndTable();
        }
    }

    private static ActionType GetFishingActionType(uint _) => ActionType.Action;

    private static readonly (string Field, uint Id, ActionType Type)[] FshActions =
        typeof(IDs.Actions).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Select(f => (Field: f.Name, Id: Convert.ToUInt32(f.GetValue(null) ?? 0u)))
            .Where(x => x.Id != IDs.Actions.None)
            .Select(x => (x.Field, x.Id, GetFishingActionType(x.Id)))
            .OrderBy(x => x.Field)
            .ToArray();

    private static string StatusName(uint id) {
        return id switch {
            IDs.Status.FoodBuff => "FoodBuff",
            IDs.Status.FishersIntuition => "FishersIntuition",
            IDs.Status.SurfaceSlap => "SurfaceSlap",
            IDs.Status.IdenticalCast => "IdenticalCast",
            IDs.Status.AnglersFortune => "AnglersFortune",
            IDs.Status.AnglersArt => "AnglersArt",
            IDs.Status.MakeshiftBait => "MakeshiftBait",
            IDs.Status.PrizeCatch => "PrizeCatch",
            IDs.Status.NaturesBounty => "NaturesBounty",
            IDs.Status.Chum => "Chum",
            IDs.Status.CollectorsGlove => "CollectorsGlove",
            IDs.Status.Snagging => "Snagging",
            IDs.Status.Salvage => "Salvage",
            IDs.Status.FishEyes => "FishEyes",
            IDs.Status.TruthOcean => "TruthOcean",
            IDs.Status.BigGameFishing => "BigGameFishing",
            IDs.Status.AmbitiousLure => "AmbitiousLure",
            IDs.Status.ModestLure => "ModestLure",
            _ => "?",
        };
    }

    private static readonly (uint Id, ActionType Type, string Label)[] KnownActionIds =
    [
        (IDs.Actions.Cast, ActionType.Action, "Cast"),
        (IDs.Actions.Mooch, ActionType.Action, "Mooch"),
        (IDs.Actions.Mooch2, ActionType.Action, "Mooch2"),
        (IDs.Actions.Hook, ActionType.Action, "Hook"),
        (IDs.Actions.Patience, ActionType.Action, "Patience"),
        (IDs.Actions.Chum, ActionType.Action, "Chum"),
        (IDs.Actions.PrizeCatch, ActionType.Action, "PrizeCatch"),
        (IDs.Actions.MultiHook, ActionType.Action, "MultiHook"),
    ];

    private static readonly (uint Id, string Label)[] KnownItemIds =
    [
        (IDs.Item.Cordial, "Cordial"),
        (IDs.Item.HQCordial, "HQCordial"),
        (IDs.Item.HiCordial, "HiCordial"),
        (IDs.Item.WateredCordial, "WateredCordial"),
        (IDs.Item.HQWateredCordial, "HQWateredCordial"),
    ];

    private static string _nmToastTitle = "AutoHook test";
    private static string _nmToastText = "Debug notification";
    private static string _nmLastResult = "";

    private static readonly NotificationConfig _nmTestConfig = new() {
        Enabled = true,
        BeepOnSuccess = true,
        DisplayToastNotification = true,
    };

    private static void DrawNotificationMaster() {
        if (!ImGui.CollapsingHeader("NotificationMaster"))
            return;

        using (ImRaii.PushIndent()) {
            var hasPlugin = Svc.Interface.IsPluginLoaded("NotificationMaster");
            var ipcReady = Service.NotificationMaster.IsIPCReady();
            ImGui.Text($"Plugin loaded: {hasPlugin}");
            ImGui.Text($"IPC ready: {ipcReady}");
            if (!hasPlugin)
                ImGui.TextDisabled("Install NotificationMaster to test IPC calls.");

            ImGui.Spacing();
            ImGui.SetNextItemWidth(320.Scaled());
            ImGui.InputText("Toast title", ref _nmToastTitle, 128);
            ImGui.SetNextItemWidth(320.Scaled());
            ImGui.InputText("Toast text", ref _nmToastText, 260);

            ImGui.Spacing();
            if (ImGui.Button("IsIPCReady"))
                SetNmResult(Service.NotificationMaster.IsIPCReady());
            ImGui.SameLine();
            if (ImGui.Button("DisplayTrayNotification"))
                SetNmResult(Service.NotificationMaster.DisplayTrayNotification(_nmToastTitle, _nmToastText));
            ImGui.SameLine();
            if (ImGui.Button("FlashTaskbarIcon"))
                SetNmResult(Service.NotificationMaster.FlashTaskbarIcon());

            if (ImGui.Button("TryBringGameForeground"))
                SetNmResult(Service.NotificationMaster.TryBringGameForeground());

            ImGui.Spacing();
            ImGui.TextUnformatted("Notify() (config-driven):");
            DrawUtil.Checkbox("Enabled", ref _nmTestConfig.Enabled);
            DrawUtil.Checkbox("Display toast", ref _nmTestConfig.DisplayToastNotification);
            DrawUtil.Checkbox("Flash taskbar", ref _nmTestConfig.FlashTaskbarIcon);
            DrawUtil.Checkbox("Bring foreground", ref _nmTestConfig.BringGameForeground);
            DrawUtil.Checkbox("Beep on success", ref _nmTestConfig.BeepOnSuccess);
            _nmTestConfig.ToastText = _nmToastText;
            if (ImGui.Button("Notify"))
                SetNmResult(Service.NotificationMaster.TryNotify(_nmTestConfig));

            if (!string.IsNullOrEmpty(_nmLastResult)) {
                ImGui.Spacing();
                ImGui.TextWrapped($"Last result: {_nmLastResult}");
            }
        }
    }

    private static void SetNmResult(bool success) => _nmLastResult = success ? "true" : "false";

    private static void DrawWikiPresets() {
        if (!ImGui.CollapsingHeader("Get Wiki presets", ImGuiTreeNodeFlags.DefaultOpen))
            return;

        using (ImRaii.Group()) {
            if (ImGui.Button($"Get Wiki info (cd: {EzThrottler.GetRemainingTime("WikiUpdate")})")) {
                _ = WikiPresets.ListWikiPages();
            }

            foreach (var preset in WikiPresets.Presets) {
                ImGui.TextWrapped($"Preset: {preset.Key}, Qtd: {preset.Value.Count}");
                foreach (var item in preset.Value)
                    ImGui.TextWrapped($"-> {item.Presets.FirstOrDefault()?.PresetName ?? "No preset name"}");
                DrawUtil.SpacingSeparator();
            }
        }
    }

    public override void Dispose() {
    }
}
