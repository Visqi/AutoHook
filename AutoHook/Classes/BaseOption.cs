namespace AutoHook.Classes;

public abstract class BaseOption
{
    public Guid UniqueId { get; private set; } = Guid.NewGuid();

    public abstract void DrawOptions();
}