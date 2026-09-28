using AutoHook.Replay;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using System.Diagnostics;
using System.IO;
using System.Numerics;

namespace AutoHook.Ui;

public sealed class ReplayManagementWindow : Window, IDisposable {
    private string _folderError = "";

    public ReplayManagementWindow() : base("Replay recorder###AutoHookReplayRecorder") {
        Service.WindowSystem.AddWindow(this);
        Size = new Vector2(640, 360);
        SizeCondition = ImGuiCond.FirstUseEver;
        RespectCloseHotkey = false;
    }

    public void Dispose() => Service.WindowSystem.RemoveWindow(this);

    public override void Draw() {
        if (!IsOpen)
            return;

        try {
            DrawRecordingRow();
            ImGui.Separator();
            DrawReplayBrowser(Service.ReplayManager);
        }
        catch (Exception e) {
            Svc.Log.Error($"[ReplayManagement] {e.Message}");
        }
    }

    private void DrawRecordingRow() {
        var mgr = Service.ReplayManager;

        if (ImGui.Button(mgr.IsRecording ? "Stop recording" : "Start recording")) {
            if (mgr.IsRecording)
                mgr.StopRecording();
            else
                mgr.StartRecording(manual: true);
        }

        ImGui.SameLine();
        if (ImGui.Button("Open replay folder"))
            _folderError = OpenDirectory(mgr.ReplayDirectory);

        if (_folderError.Length > 0) {
            ImGui.SameLine();
            using var color = ImRaii.PushColor(ImGuiCol.Text, 0xff0000ff);
            ImGui.Text(_folderError);
        }

        if (!string.IsNullOrEmpty(mgr.LastRecordedPath))
            ImGui.TextWrapped($"Last file: {mgr.LastRecordedPath}");
    }

    private static void DrawReplayBrowser(ReplayManager mgr) {
        DrawNewEntry(mgr);
        DrawEntries(mgr);
        DrawEntriesOperations(mgr);
    }

    private static void DrawNewEntry(ReplayManager mgr) {
        var path = mgr.BrowserPath;
        ImGui.InputText("###path", ref path, 500);
        mgr.BrowserPath = path;
        ImGui.SameLine();
        if (ImGuiComponents.IconButton(FontAwesomeIcon.File)) {
            Service.FileDialog.OpenFileDialog("Select replay", ".ahlog", (confirmed, paths) => {
                if (confirmed && paths.Count > 0) {
                    mgr.BrowserPath = paths[0];
                    mgr.FileDialogStartPath = new FileInfo(mgr.BrowserPath).Directory!.FullName;
                }
            }, 1, mgr.FileDialogStartPath);
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Open file");
        ImGui.SameLine();
        using (ImRaii.Disabled(mgr.BrowserPath.Length == 0 || mgr.Entries.Any(e => e.Path == mgr.BrowserPath))) {
            if (ImGui.Button("Open"))
                mgr.AddEntry(mgr.BrowserPath, autoShow: true);
        }
    }

    private static void DrawEntries(ReplayManager mgr) {
        using var table = ImRaii.Table("replay_entries", 3, ImGuiTableFlags.Resizable);
        if (!table)
            return;

        ImGui.TableSetupColumn("op", ImGuiTableColumnFlags.WidthFixed, 100);
        ImGui.TableSetupColumn("unload", ImGuiTableColumnFlags.WidthFixed, 50);

        foreach (var e in mgr.Entries) {
            using var idScope = ImRaii.PushId(e.Path);

            ImGui.TableNextColumn();
            if (!e.Replay.IsCompleted) {
                ImGui.ProgressBar(e.Progress, new Vector2(100, 0));
            }
            else if (e.Replay.IsFaulted || e.Replay.Result.Ops.Count == 0) {
                using var color = ImRaii.PushColor(ImGuiCol.Text, 0xff0000ff);
                ImGui.Text("(failed)");
            }
            else {
                if (ImGui.Button("Actions...", new Vector2(100, 0)))
                    ImGui.OpenPopup("ctx");
                using var popup = ImRaii.Popup("ctx");
                if (popup) {
                    if (ImGui.MenuItem("Show"))
                        e.Show();
                }
            }

            ImGui.TableNextColumn();
            if (ImGui.Button(e.Replay.IsCompleted ? "Unload" : "Cancel", new Vector2(50, 0)))
                e.Dispose();

            ImGui.TableNextColumn();
            ImGui.Checkbox($"{e.Path}", ref e.Selected);
        }
    }

    private static void DrawEntriesOperations(ReplayManager mgr) {
        if (mgr.Entries.Count == 0)
            return;

        var numSelected = mgr.Entries.Count(e => e.Selected);
        var shouldSelectAll = numSelected < mgr.Entries.Count;
        if (ImGui.Button(shouldSelectAll ? "Select all" : "Unselect all", new Vector2(80, 0))) {
            foreach (var e in mgr.Entries)
                e.Selected = shouldSelectAll;
        }
        using (ImRaii.Disabled(numSelected == 0)) {
            ImGui.SameLine();
            if (ImGui.Button("Show selected")) {
                foreach (var e in mgr.Entries.Where(e => e.Selected))
                    e.Show();
            }
            ImGui.SameLine();
            if (ImGui.Button("Unload selected")) {
                foreach (var e in mgr.Entries.Where(e => e.Selected).ToList())
                    e.Dispose();
            }
        }
        ImGui.SameLine();
        if (ImGui.Button("Unload all")) {
            foreach (var e in mgr.Entries.ToList())
                e.Dispose();
        }
    }

    private static string OpenDirectory(DirectoryInfo dir) {
        if (!dir.Exists)
            return $"Directory '{dir}' not found.";
        try {
            Process.Start(new ProcessStartInfo(dir.FullName) { UseShellExecute = true });
            return "";
        }
        catch (Exception e) {
            Svc.Log.Error($"[Replay] Failed to open {dir}: {e.Message}");
            return $"Failed to open folder; open it manually.";
        }
    }
}
