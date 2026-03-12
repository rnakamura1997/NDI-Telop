using System.Text.Json.Serialization;

namespace NdiTelop.Models;

public class AppSettings
{
    public NdiConfig Ndi { get; set; } = new();
    public int WebApiPort { get; set; } = 5000;
    public int OscPort { get; set; } = 8000;

    [JsonIgnore]
    public int HttpPort
    {
        get => WebApiPort;
        set => WebApiPort = value;
    }

    [JsonIgnore]
    public int OscReceivePort
    {
        get => OscPort;
        set => OscPort = value;
    }
}
