using System.Text.Json.Serialization;

namespace NdiTelop.Models;

public class AppSettings
{
    public NdiConfig Ndi { get; set; } = new();
    public RemoteControlSettings RemoteControl { get; set; } = new();
    public string AssetPath { get; set; } = Path.Combine(AppContext.BaseDirectory, "data", "assets");

    public HotkeySettings Hotkeys { get; set; } = new();

    public ThemeSettings Theme { get; set; } = new();

    public OutputSettings Output { get; set; } = new();

    public LogViewerSettings LogViewer { get; set; } = new();

    [JsonIgnore]
    public int HttpPort
    {
        get => RemoteControl.WebApiPort;
        set => RemoteControl.WebApiPort = value;
    }

    [JsonIgnore]
    public int OscReceivePort
    {
        get => RemoteControl.OscPort;
        set => RemoteControl.OscPort = value;
    }

    [JsonIgnore]
    public int WebApiPort
    {
        get => RemoteControl.WebApiPort;
        set => RemoteControl.WebApiPort = value;
    }

    [JsonIgnore]
    public int OscPort
    {
        get => RemoteControl.OscPort;
        set => RemoteControl.OscPort = value;
    }
}
