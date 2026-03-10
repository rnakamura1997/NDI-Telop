namespace NdiTelop.Interfaces;
public interface IWebApiService
{
    int Port { get; set; }
    Task StartAsync();
    Task StopAsync();
}
