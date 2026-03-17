namespace NdiTelop.Models;

public class LogViewerSettings
{
    public bool ShowDebug { get; set; } = true;
    public bool ShowInformation { get; set; } = true;
    public bool ShowWarning { get; set; } = true;
    public bool ShowError { get; set; } = true;
    public bool ShowFatal { get; set; } = true;
    public string Keyword { get; set; } = string.Empty;
    public bool AutoScroll { get; set; } = true;
}
