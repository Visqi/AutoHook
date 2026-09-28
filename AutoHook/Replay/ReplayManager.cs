using AutoHook.Ui;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;
using Newtonsoft.Json;
using System.IO;
using System.Reflection;
using System.Threading;
using TerritoryIntendedUse = FFXIVClientStructs.FFXIV.Client.Enums.TerritoryIntendedUse;

namespace AutoHook.Replay;

public sealed class ReplayManager : IDisposable {
    private const int MaxReplayFiles = 10;

    public sealed class ReplayEntry : IDisposable {
        public string Path;
        public float Progress;
        public CancellationTokenSource Cancel = new();
        public Task<FishingReplay> Replay;
        public ReplayDetailsWindow? Window;
        public bool AutoShowWindow;
        public bool Selected;
        public bool Disposed;
        public bool Disposing;
        public DateTime? InitialTime;

        public ReplayEntry(string path, bool autoShow, DateTime? initialTime = null) {
            Path = path;
            AutoShowWindow = autoShow;
            InitialTime = initialTime;
            Replay = Task.Run(() => ReplayParser.Parse(path, ref Progress, Cancel.Token));
        }

        public void Dispose() {
            Disposing = true;
            Window?.Dispose();
            Cancel.Cancel();
            try {
                Replay.Wait();
            }
            catch { }
            Replay.Dispose();
            Cancel.Dispose();
            Disposed = true;
        }

        public void Show() {
            if (!Replay.IsCompletedSuccessfully || Replay.Result.Ops.Count == 0)
                return;
            Window ??= new ReplayDetailsWindow(Replay.Result, InitialTime);
            Window.IsOpen = true;
            Window.BringToFront();
        }
    }

    private ReplayRecorder? _recorder;
    private readonly EventSubscriptions _subs;
    private readonly List<ReplayEntry> _entries = [];
    private AutoGigConfig? _recordingSpearfishingPreset;
    private int _stopAfterFrames;
    private uint _lastTerritoryId;

    public bool IsRecording => _recorder != null;
    public string? LastRecordedPath { get; private set; }
    public DirectoryInfo ReplayDirectory { get; }
    public IReadOnlyList<ReplayEntry> Entries => _entries;
    public string BrowserPath { get; set; } = "";
    public string FileDialogStartPath { get; set; }

    public ReplayManager() {
        var dir = Path.Combine(Svc.Interface.GetPluginConfigDirectory(), "replays");
        ReplayDirectory = new DirectoryInfo(dir);
        ReplayDirectory.Create();
        FileDialogStartPath = ReplayDirectory.FullName;
        BrowserPath = ReplayDirectory.FullName;

        var ws = Service.WorldState;
        _subs = new(
            ws.BeganSession.Subscribe(_ => TryAutoStart()),
            ws.TerritoryChanged.Subscribe(OnTerritoryChanged),
            ws.OceanZoneStarted.Subscribe(_ => TryAutoStart()),
            ws.EndedSession.Subscribe(_ => TryAutoStop()),
            ws.SpearfishingSessionStarted.Subscribe(_ => TryAutoStart()),
            ws.SpearfishingSessionEnded.Subscribe(_ => TryAutoStop()));

        Svc.Framework.Update += OnFrameworkUpdate;
        PruneOldReplays();
    }

    private void OnFrameworkUpdate(IFramework _) {
        _recorder?.FlushPending();
        if (_stopAfterFrames > 0 && --_stopAfterFrames == 0)
            StopRecording();
        Update();
    }

    public void Dispose() {
        Svc.Framework.Update -= OnFrameworkUpdate;
        _stopAfterFrames = 0;
        StopRecording();
        _subs.Dispose();
        foreach (var e in _entries)
            e.Dispose();
        _entries.Clear();
    }

    public void Update() {
        _entries.RemoveAll(e => e.Disposed);

        foreach (var e in _entries) {
            if (e.AutoShowWindow && e.Window == null && e.Replay.IsCompletedSuccessfully && e.Replay.Result.Ops.Count > 0)
                e.Show();
        }
    }

    public void StartRecording(bool manual = true) {
        if (_recorder != null)
            return;

        var prefix = manual ? "manual" : "session";
        _recordingSpearfishingPreset = GetActiveSpearfishingPreset();
        _recorder = new ReplayRecorder(Service.WorldState, ReplayDirectory, prefix, logInitialState: true);
        _recorder.WritePresetSnapshot(SerializeCurrentPreset(_recordingSpearfishingPreset));
        LastRecordedPath = _recorder.FilePath;
        Service.PrintDebug($"[Replay] Recording started: {_recorder.FilePath}");
    }

    public void StopRecording() {
        if (_recorder is not { } recorder)
            return;

        recorder.FlushPending();
        recorder.WriteMeta(BuildMetadata(_recordingSpearfishingPreset));
        LastRecordedPath = recorder.FilePath;
        recorder.Dispose();
        _recorder = null;
        _recordingSpearfishingPreset = null;
        PruneOldReplays();
        Service.PrintDebug($"[Replay] Recording stopped: {LastRecordedPath}");
    }

    public void AddEntry(string path, bool autoShow) {
        CleanPath(ref path);
        if (path.Length == 0 || _entries.Any(e => e.Path == path))
            return;
        if (!File.Exists(path))
            return;
        _entries.Add(new ReplayEntry(path, autoShow));
        BrowserPath = path;
    }

    private static void CleanPath(ref string path) {
        path = path.Trim();
        if (path.StartsWith('"') && path.EndsWith('"'))
            path = path[1..^1];
    }

    private void TryAutoStart() {
        _stopAfterFrames = 0;
        if (_recorder == null)
            StartRecording(manual: false);
    }

    private void TryAutoStop() {
        if (_recorder == null)
            return;
        // Session events fire before Modified enqueues their operation, so stop next frame to capture it.
        _stopAfterFrames = 1;
    }

    private void OnTerritoryChanged(WorldState.OpTerritory op) {
        static bool IsOcean(uint territoryId) => territoryId != 0 && TerritoryType.GetRow(territoryId).TerritoryIntendedUse.Value.StructsEnum is TerritoryIntendedUse.OceanFishing;
        var wasOcean = IsOcean(_lastTerritoryId);
        var isOcean = IsOcean(op.TerritoryId);
        _lastTerritoryId = op.TerritoryId;

        if (isOcean)
            TryAutoStart();
        else if (wasOcean)
            TryAutoStop();
    }

    private void PruneOldReplays() {
        if (!ReplayDirectory.Exists)
            return;

        foreach (var file in ReplayDirectory.GetFiles("*.ahlog")
                     .OrderByDescending(f => f.LastWriteTimeUtc)
                     .Skip(MaxReplayFiles)) {
            try {
                file.Delete();
                Service.PrintDebug($"[Replay] Pruned old replay: {file.Name}");
            }
            catch (Exception e) {
                Svc.Log.Warning($"[Replay] Failed to delete {file.FullName}: {e.Message}");
            }
        }
    }

    private static ReplayMetadata BuildMetadata(AutoGigConfig? recordedSpearPreset) {
        var cfg = Service.Configuration;
        return new ReplayMetadata {
            PresetName = recordedSpearPreset?.PresetName ?? cfg.HookPresets.CurrentPreset.PresetName,
            PluginVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? string.Empty,
            TerritoryId = Service.WorldState.TerritoryId,
            PresetSnapshotJson = SerializeCurrentPreset(recordedSpearPreset),
        };
    }

    private static string SerializeCurrentPreset(AutoGigConfig? spearPreset) {
        BasePresetConfig preset = spearPreset is not null
            ? spearPreset
            : Service.Configuration.HookPresets.CurrentPreset;
        try {
            return JsonConvert.SerializeObject(preset);
        }
        catch (Exception e) {
            Svc.Log.Warning($"[Replay] Failed to serialize preset snapshot: {e.Message}");
            return string.Empty;
        }
    }

    private static AutoGigConfig? GetActiveSpearfishingPreset()
        => Service.WorldState.Spearfishing.SessionActive ? Service.Configuration.AutoGigConfig.SelectedPreset : null;
}
