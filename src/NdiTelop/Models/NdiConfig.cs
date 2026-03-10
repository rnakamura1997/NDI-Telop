namespace NdiTelop.Models;

public class NdiConfig
{
    public string ProgramSourceName { get; set; } = "NdiTelop Program";
    public string PreviewSourceName { get; set; } = "NdiTelop Preview";
    public int Width { get; set; } = 1920;
    public int Height { get; set; } = 1080;
    public double FrameRate { get; set; } = 59.94;
}
