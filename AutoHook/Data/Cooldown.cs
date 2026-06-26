namespace AutoHook.Data;

public struct Cooldown(float elapsed, float total) : IEquatable<Cooldown> {
    public float Elapsed = elapsed;
    public float Total = total;

    public readonly float Remaining => Total - Elapsed;

    public override readonly string ToString() => $"{Elapsed:f3}/{Total:f3}";

    public readonly bool Equals(Cooldown other) => Elapsed.Equals(other.Elapsed) && Total.Equals(other.Total);
    public override readonly bool Equals(object? obj) => obj is Cooldown other && Equals(other);
    public override readonly int GetHashCode() => HashCode.Combine(Elapsed, Total);
    public static bool operator ==(Cooldown left, Cooldown right) => left.Equals(right);
    public static bool operator !=(Cooldown left, Cooldown right) => !left.Equals(right);
}
