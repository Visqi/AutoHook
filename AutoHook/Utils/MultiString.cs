using Lumina.Excel.Sheets;
using Lumina.Text.ReadOnly;
using Action = Lumina.Excel.Sheets.Action;

namespace AutoHook.Utils;

public readonly struct MultiString
{
    public static string ParseSeString(ReadOnlySeString? luminaString)
        => luminaString?.ExtractText() ?? string.Empty;

    public static string GetStatusName(uint statusId)
    {
        return ParseSeString(GetRow<Status>(statusId)?.Name);
    }

    public static string GetActionName(uint id)
    {
        return ParseSeString(GetRow<Action>(id)?.Name);
    }

    public static string GetItemName(uint id)
    {
        string itemName = string.Empty;
        try
        {
            itemName = ParseSeString(GetRow<Item>(id)?.Name);
        }
        catch (Exception e)
        {
            Svc.Log.Error(e.Message);
        }
        if (id == 0)
            return UIStrings.None;

        return itemName == string.Empty ? UIStrings.None : itemName;
    }

    public static string GetItemName(int id)
    {
        return GetItemName((uint)id);
    }
}
