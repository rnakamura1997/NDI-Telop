namespace NdiTelop.Models;

public class PlaylistStatusSnapshot
{
    public int CurrentIndex { get; set; } = -1;
    public bool IsRunning { get; set; }
    public bool AutoAdvanceEnabled { get; set; }
    public int RemainingSeconds { get; set; }
    public string? CurrentPresetId { get; set; }
    public string CurrentPresetName { get; set; } = string.Empty;
    public string? NextPresetId { get; set; }
    public string NextPresetName { get; set; } = string.Empty;
    public List<PlaylistStatusItem> Items { get; set; } = [];
}

public class PlaylistStatusItem
{
    public int Index { get; set; }
    public string PresetId { get; set; } = string.Empty;
    public string PresetName { get; set; } = string.Empty;
    public int DisplayDurationSeconds { get; set; }
}
