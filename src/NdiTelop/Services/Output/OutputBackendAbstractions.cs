namespace NdiTelop.Services.Output;

public enum OutputLifecycleState
{
    NotStarted,
    Starting,
    Started,
    Stopped,
}

public interface IOutputBackend
{
    string BackendName { get; }

    Task StartAsync(OutputStartContext context, CancellationToken cancellationToken = default);

    Task StopAsync(CancellationToken cancellationToken = default);

    Task SendAsync(ReadOnlyMemory<byte> payload, CancellationToken cancellationToken = default);

    IReadOnlyList<string> GetAvailableDevices();
}

public sealed record OutputStartContext(int? DeviceIndex = null, string? SenderName = null);

public sealed class NoOpOutputBackend(string backendName) : IOutputBackend
{
    public string BackendName { get; } = backendName;

    public Task StartAsync(OutputStartContext context, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task SendAsync(ReadOnlyMemory<byte> payload, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public IReadOnlyList<string> GetAvailableDevices() => Array.Empty<string>();
}
