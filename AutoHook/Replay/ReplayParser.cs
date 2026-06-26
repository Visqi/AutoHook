using System.IO;
using System.IO.Compression;
using System.Threading;

namespace AutoHook.Replay;

public static class ReplayParser {
    public static FishingReplay Parse(string path) {
        var progress = 0f;
        return Parse(path, ref progress, CancellationToken.None);
    }

    public static FishingReplay Parse(string path, ref float progress, CancellationToken cancel) {
        var replay = new FishingReplay { SourcePath = path };
        using var rawStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        Span<byte> header = stackalloc byte[4];
        if (rawStream.Read(header) != header.Length)
            return replay;

        if (BitConverter.ToUInt32(header) != ReplayLogFormatMagic.CompressedBinary)
            return replay;

        using var brotli = new BrotliStream(rawStream, CompressionMode.Decompress, leaveOpen: false);
        using var reader = new ReplayBinaryReader(brotli, replay, cancel);
        reader.ParseAll();
        progress = reader.Progress;
        return replay;
    }
}
