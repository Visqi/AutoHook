using Dalamud.Game.Gui.Dtr;
using Dalamud.Game.Text.SeStringHandling;
using ECommons.Reflection;

namespace AutoHook.Utils;

public class EzDtr2 : IDisposable
{
    public IDtrBarEntry? Entry;
    internal static List<EzDtr2> Registered = [];
    internal Func<SeString> Text;
    internal Action<DtrInteractionEvent>? OnClick;
    internal Func<bool>? ShowCondition;

    /// <summary>
    /// Creates a new <see cref="EzDtr"/>
    /// </summary>
    /// <param name="text">Function that returns an <see cref="SeString"/> for the entry's text.</param>
    /// <param name="onClick">Action performed whenever the entry is clicked</param>
    /// <param name="title">Name of the Dtr entry. Defaults to the plugin name.</param>
    public EzDtr2(Func<SeString> text, Action? onClick = null, string? title = null, Func<bool> showCondition = null)
    {
        title ??= DalamudReflector.GetPluginName();
        Text = text;
        OnClick = onClick != null ? _ => onClick() : null;
        Entry ??= Svc.DtrBar.Get(title);
        ShowCondition = showCondition;
        Svc.Framework.Update += OnUpdate;
        Registered.Add(this);
    }

    public EzDtr2(Func<SeString> text, Action<DtrInteractionEvent>? onClick = null, string? title = null, Func<bool> showCondition = null)
    {
        title ??= DalamudReflector.GetPluginName();
        Text = text;
        OnClick = onClick;
        Entry ??= Svc.DtrBar.Get(title);
        ShowCondition = showCondition;
        Svc.Framework.Update += OnUpdate;
        Registered.Add(this);
    }

    internal void OnUpdate(object _)
    {
        if (Entry.Shown)
        {
            if (ShowCondition != null && !ShowCondition())
            {
                Entry.Text = string.Empty;
                return;
            }
            Entry.Text = Text();
            if (OnClick != null)
                Entry.OnClick = OnClick;
        }
    }

    public void Dispose()
    {
        Svc.Framework.Update -= OnUpdate;
        Registered.Remove(this);
        Entry.Remove();
    }

    public static void DisposeAll() => Registered.ToArray().Each(x => x.Dispose());
}
