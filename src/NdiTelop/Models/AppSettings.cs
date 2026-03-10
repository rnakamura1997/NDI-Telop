namespace NdiTelop.Models;

public class AppSettings
{
    public NdiConfig Ndi { get; set; } = new();
    public int HttpPort { get; set; } = 8080;
    public int OscReceivePort { get; set; } = 57120;
}
