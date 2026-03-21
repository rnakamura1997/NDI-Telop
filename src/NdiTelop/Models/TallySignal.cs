namespace NdiTelop.Models;

public class TallySignal
{
    public string Source { get; set; } = string.Empty;
    public string RemoteIpAddress { get; set; } = string.Empty;
    public string Transport { get; set; } = string.Empty;
    public bool Program { get; set; }
    public bool Preview { get; set; }
    public string Bus { get; set; } = string.Empty;
    public string Metadata { get; set; } = string.Empty;
    public DateTimeOffset ReceivedAt { get; set; } = DateTimeOffset.UtcNow;
}
