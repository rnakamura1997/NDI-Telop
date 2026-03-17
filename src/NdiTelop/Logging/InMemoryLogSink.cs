using Avalonia.Threading;
using Serilog.Core;
using Serilog.Events;
using System.Collections.ObjectModel;

namespace NdiTelop.Logging;

public class InMemoryLogSink : ILogEventSink
{
    private readonly int _maxEntries;

    public ObservableCollection<RecentLogEntry> RecentLogs { get; } = [];

    public InMemoryLogSink(int maxEntries = 200)
    {
        _maxEntries = maxEntries;
    }

    public void Emit(LogEvent logEvent)
    {
        var entry = new RecentLogEntry
        {
            Timestamp = logEvent.Timestamp,
            Level = logEvent.Level,
            Message = logEvent.RenderMessage()
        };

        if (Dispatcher.UIThread.CheckAccess())
        {
            AddMessage(entry);
            return;
        }

        Dispatcher.UIThread.Post(() => AddMessage(entry));
    }

    private void AddMessage(RecentLogEntry entry)
    {
        RecentLogs.Add(entry);
        while (RecentLogs.Count > _maxEntries)
        {
            RecentLogs.RemoveAt(0);
        }
    }
}
