using System.IO;
using System.IO.Compression;

namespace AutoHook.Replay;

public sealed class ReplayRecorder : IDisposable {
    private readonly WorldState _ws;
    private readonly ReplayOutput _logger;
    private readonly Stream _stream;
    private readonly Queue<WorldState.Operation> _pendingOps = new();
    private readonly object _pendingLock = new();

    public string FilePath { get; }

    public ReplayRecorder(WorldState ws, DirectoryInfo targetDirectory, string logPrefix, bool logInitialState) {
        _ws = ws;
        targetDirectory.Create();
        FilePath = Path.Combine(targetDirectory.FullName, $"{logPrefix}_{DateTime.UtcNow:yyyy_MM_dd_HH_mm_ss}.ahlog");

        _stream = new FileStream(FilePath, FileMode.Create, FileAccess.Write, FileShare.Read);
        WriteMagic(_stream, ReplayLogFormatMagic.CompressedBinary);
        var compressed = new BrotliStream(_stream, CompressionLevel.Optimal, leaveOpen: false);
        _logger = new ReplayBinaryOutput(compressed);
        _ws.Modified += Enqueue;

        WriteHeader(logInitialState);
    }

    private static void WriteMagic(Stream stream, uint magic) {
        Span<byte> buf = stackalloc byte[4];
        BitConverter.TryWriteBytes(buf, magic);
        stream.Write(buf);
    }

    private void WriteHeader(bool logInitialState) {
        var start = _ws.CurrentTime != default ? _ws.CurrentTime : DateTime.UtcNow;
        _logger.StartEntry(start);
        _logger.EmitFourCC("VER ")
            .Emit(ReplayLogFormatMagic.Version)
            .Emit(_ws.QPF)
            .Emit(_ws.GameVersion)
            .Emit(start.Ticks);
        _logger.EndEntry();

        if (!logInitialState)
            return;

        var ts = start;
        foreach (var op in _ws.CompareToInitial()) {
            op.Timestamp = ts;
            WriteOpDirect(op, ts);
        }
    }

    public void WritePresetSnapshot(string presetJson) {
        if (string.IsNullOrEmpty(presetJson))
            return;

        var ts = _ws.CurrentTime != default ? _ws.CurrentTime : DateTime.UtcNow;
        _logger.StartEntry(ts);
        _logger.EmitFourCC("PSNP").Emit(presetJson);
        _logger.EndEntry();
    }

    public void WriteMeta(ReplayMetadata meta) {
        var ts = _ws.CurrentTime != default ? _ws.CurrentTime : DateTime.UtcNow;
        _logger.StartEntry(ts);
        _logger.EmitFourCC("META")
            .Emit(meta.PresetName)
            .Emit(meta.PluginVersion)
            .Emit(meta.TerritoryId);
        _logger.EndEntry();
    }

    public void FlushPending() {
        WorldState.Operation[] batch;
        lock (_pendingLock) {
            if (_pendingOps.Count == 0)
                return;
            batch = [.. _pendingOps];
            _pendingOps.Clear();
        }

        foreach (var op in batch) {
            try {
                WriteOpDirect(op, op.Timestamp);
            }
            catch (Exception e) {
                Svc.Log.Error(e, "[Replay] Failed to log operation");
            }
        }
    }

    public void Dispose() {
        _ws.Modified -= Enqueue;
        FlushPending();
        _logger.Flush();
        _logger.Dispose();
        _stream.Dispose();
    }

    private void Enqueue(WorldState.Operation op) {
        lock (_pendingLock)
            _pendingOps.Enqueue(op);
    }

    private void WriteOpDirect(WorldState.Operation op, DateTime timestamp) {
        if (op.Timestamp == default)
            op.Timestamp = timestamp;
        _logger.StartEntry(op.Timestamp);
        op.Write(_logger);
        _logger.EndEntry();
    }
}
