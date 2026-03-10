namespace NdiTelop.Interfaces;
public interface IOscService
{
    int ReceivePort { get; set; }
    Task StartAsync();
    Task StopAsync();
    Task SendFeedbackAsync(string address, params object[] args);
}
