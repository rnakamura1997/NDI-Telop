namespace NdiTelop.Models;

public class OutputSettings
{
    public OutputBackendType SelectedBackend { get; set; } = OutputBackendType.Ndi;

    public string SpoutSenderName { get; set; } = "NdiTelop-Spout2";

    public int DeckLinkDeviceIndex { get; set; }
}
