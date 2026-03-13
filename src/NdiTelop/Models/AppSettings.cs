using System.Text.Json.Serialization;

namespace NdiTelop.Models;

public class AppSettings
{
    public NdiConfig Ndi { get; set; } = new();
    public int WebApiPort { get; set; } = 5000;
    public int OscPort { get; set; } = 8000;
    public string AssetPath { get; set; } = Path.Combine(AppContext.BaseDirectory, "data", "assets");

    public HotkeySettings Hotkeys { get; set; } = new();

    public ThemeSettings Theme { get; set; } = new();

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
