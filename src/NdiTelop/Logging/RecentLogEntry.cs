using Serilog.Events;

namespace NdiTelop.Logging;

public sealed class RecentLogEntry
{
    public required DateTimeOffset Timestamp { get; init; }
    public required LogEventLevel Level { get; init; }
    public required string Message { get; init; }

    public string Formatted => $"[{Timestamp:HH:mm:ss}] {Level}: {Message}";
}
