using Avalonia.Threading;
using Serilog.Core;
using Serilog.Events;
using System.Collections.ObjectModel;

namespace NdiTelop.Logging;

public class InMemoryLogSink : ILogEventSink
{
    private readonly int _maxEntries;

    public ObservableCollection<string> RecentLogs { get; } = [];

    public InMemoryLogSink(int maxEntries = 8)
    {
        _maxEntries = maxEntries;
    }

    public void Emit(LogEvent logEvent)
    {
        var message = $"[{logEvent.Timestamp:HH:mm:ss}] {logEvent.Level}: {logEvent.RenderMessage()}";

        if (Dispatcher.UIThread.CheckAccess())
        {
            AddMessage(message);
            return;
        }

        Dispatcher.UIThread.Post(() => AddMessage(message));
    }

    private void AddMessage(string message)
    {
        RecentLogs.Add(message);
        while (RecentLogs.Count > _maxEntries)
        {
            RecentLogs.RemoveAt(0);
        }
    }
}
