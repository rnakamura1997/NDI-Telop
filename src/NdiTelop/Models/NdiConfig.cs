namespace NdiTelop.Models;

public class NdiConfig
{
    public string SourceName { get; set; } = "NdiTelop";
    
    public int ResolutionWidth { get; set; } = 1920;
    public int ResolutionHeight { get; set; } = 1080;
    public int FrameRateN { get; set; } = 30000;
    public int FrameRateD { get; set; } = 1001;
}
