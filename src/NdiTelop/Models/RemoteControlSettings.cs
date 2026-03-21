namespace NdiTelop.Models;

public class RemoteControlSettings
{
    public string WebApiHost { get; set; } = "*";
    public int WebApiPort { get; set; } = 5000;
    public int OscPort { get; set; } = 8000;
    public string OscFeedbackHost { get; set; } = "127.0.0.1";
    public int OscFeedbackPort { get; set; } = 8000;
    public bool EnableTallyAutoTake { get; set; }
    public string TallyPartnerIpAddress { get; set; } = string.Empty;
    public string TallyPartnerName { get; set; } = string.Empty;
    public KeyerDestination TallyAutoTakeKeyer { get; set; } = KeyerDestination.Usk1;
    public bool AcceptNdiMetadataTally { get; set; } = true;
}
