namespace AutoHook.Classes;

public class PresetFolder(string folderName)
{
    public Guid UniqueId { get; set; } = Guid.NewGuid();
    public string FolderName { get; set; } = folderName;
    public bool IsExpanded { get; set; } = true;
    public List<Guid> PresetIds { get; set; } = [];

    public void AddPreset(Guid presetId)
    {
        if (!PresetIds.Contains(presetId))
            PresetIds.Add(presetId);
    }

    public void RemovePreset(Guid presetId)
    {
        if (PresetIds.Contains(presetId))
            PresetIds = [.. PresetIds.Where(p => p != presetId)];
    }

    public bool ContainsPreset(Guid presetId)
    {
        return PresetIds.Contains(presetId);
    }
}