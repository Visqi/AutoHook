using System.Diagnostics;
using System.IO;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
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
            Service.ReplayManager.Draw();
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
