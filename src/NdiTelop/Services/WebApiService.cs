using NdiTelop.Interfaces;

namespace NdiTelop.Services;

public class WebApiService : IWebApiService
{
    public int Port { get; set; } = 8080;

    public Task StartAsync() => Task.CompletedTask;
    public Task StopAsync() => Task.CompletedTask;
}
