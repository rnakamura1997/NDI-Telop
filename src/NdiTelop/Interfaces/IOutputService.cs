namespace NdiTelop.Interfaces;

public interface IOutputService
{
    Task StartVirtualCameraAsync();
    Task StopVirtualCameraAsync();
    Task StartDeckLinkOutputAsync(int deviceIndex);
    Task StopDeckLinkOutputAsync();
    Task StartSpoutAsync(string senderName);
    Task StopSpoutAsync();
    IReadOnlyList<string> GetAvailableDeckLinkDevices();
}
