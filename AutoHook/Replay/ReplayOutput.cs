using System.IO;

namespace AutoHook.Replay;

public abstract class ReplayOutput : IDisposable {
    public abstract void StartEntry(DateTime t);
    public abstract void EndEntry();
    public abstract void Flush();
    public abstract ReplayOutput EmitFourCC(string tag);
    public abstract ReplayOutput Emit();
    public abstract ReplayOutput Emit(string v);
    public abstract ReplayOutput Emit(bool v);
    public abstract ReplayOutput Emit(byte v);
    public abstract ReplayOutput Emit(sbyte v);
    public abstract ReplayOutput Emit(ushort v);
    public abstract ReplayOutput Emit(short v);
    public abstract ReplayOutput Emit(int v);
    public abstract ReplayOutput Emit(uint v);
    public abstract ReplayOutput Emit(long v);
    public abstract ReplayOutput Emit(ulong v);
    public abstract ReplayOutput Emit(float v);
    public abstract ReplayOutput Emit(double v);
    public abstract ReplayOutput Emit(TimeOnly v);

    public void Dispose() {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing) { }
}

public sealed class ReplayBinaryOutput(Stream dest) : ReplayOutput {
    private readonly BinaryWriter _writer = new(dest);

    protected override void Dispose(bool disposing) {
        if (disposing)
            _writer.Dispose();
    }

    public override void Flush() => _writer.Flush();
    public override void StartEntry(DateTime t) { }
    public override void EndEntry() { }

    public override ReplayOutput EmitFourCC(string tag) {
        _writer.Write(ReplayLogFormatMagic.ToFourCC(tag));
        return this;
    }

    public override ReplayOutput Emit() => this;
    public override ReplayOutput Emit(string v) {
        _writer.Write(v);
        return this;
    }

    public override ReplayOutput Emit(bool v) {
        _writer.Write(v);
        return this;
    }

    public override ReplayOutput Emit(byte v) {
        _writer.Write(v);
        return this;
    }

    public override ReplayOutput Emit(sbyte v) {
        _writer.Write(v);
        return this;
    }

    public override ReplayOutput Emit(ushort v) {
        _writer.Write(v);
        return this;
    }

    public override ReplayOutput Emit(short v) {
        _writer.Write(v);
        return this;
    }

    public override ReplayOutput Emit(int v) {
        _writer.Write(v);
        return this;
    }

    public override ReplayOutput Emit(uint v) {
        _writer.Write(v);
        return this;
    }

    public override ReplayOutput Emit(long v) {
        _writer.Write(v);
        return this;
    }

    public override ReplayOutput Emit(ulong v) {
        _writer.Write(v);
        return this;
    }

    public override ReplayOutput Emit(float v) {
        _writer.Write(v);
        return this;
    }

    public override ReplayOutput Emit(double v) {
        _writer.Write(v);
        return this;
    }

    public override ReplayOutput Emit(TimeOnly v) {
        _writer.Write(v.Ticks);
        return this;
    }
}
