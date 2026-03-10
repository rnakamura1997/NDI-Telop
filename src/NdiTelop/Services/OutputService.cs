using NdiTelop.Interfaces;

namespace NdiTelop.Services;

public class OutputService : IOutputService
{
    public Task StartVirtualCameraAsync() => Task.CompletedTask;
    public Task StopVirtualCameraAsync() => Task.CompletedTask;
    public Task StartDeckLinkOutputAsync(int deviceIndex) => Task.CompletedTask;
    public Task StopDeckLinkOutputAsync() => Task.CompletedTask;
    public Task StartSpoutAsync(string senderName) => Task.CompletedTask;
    public Task StopSpoutAsync() => Task.CompletedTask;
    public IReadOnlyList<string> GetAvailableDeckLinkDevices() => Array.Empty<string>();
}
