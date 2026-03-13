using Serilog;

namespace NdiTelop.Logging;

public static class AppLogger
{
    public static InMemoryLogSink InMemorySink { get; } = new();

    public static void Configure()
    {
        var logDirectory = Path.Combine(AppContext.BaseDirectory, "data", "logs");
        Directory.CreateDirectory(logDirectory);

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .Enrich.FromLogContext()
            .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
            .WriteTo.File(
                Path.Combine(logDirectory, "nditelop-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .WriteTo.Sink(InMemorySink)
            .CreateLogger();
    }
}
