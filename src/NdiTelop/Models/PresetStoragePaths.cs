namespace NdiTelop.Models;

/// <summary>
/// プリセット保存パス定義。
/// </summary>
public sealed class PresetStoragePaths
{
    public string PresetDirectoryPath { get; init; }
    public string DefaultPresetsFilePath { get; init; }

    public PresetStoragePaths(string presetDirectoryPath, string defaultPresetsFilePath)
    {
        PresetDirectoryPath = presetDirectoryPath;
        DefaultPresetsFilePath = defaultPresetsFilePath;
    }
}
