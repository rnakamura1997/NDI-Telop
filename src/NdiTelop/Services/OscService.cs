using NdiTelop.Interfaces;

namespace NdiTelop.Services;

public class OscService : IOscService
{
    public int ReceivePort { get; set; } = 57120;

    public Task StartAsync() => Task.CompletedTask;
    public Task StopAsync() => Task.CompletedTask;
    public Task SendFeedbackAsync(string address, params object[] args) => Task.CompletedTask;
}
