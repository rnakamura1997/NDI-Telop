namespace NdiTelop.Models;

public class AnimationConfig
{
    public string InType { get; set; } = "cut";
    public string OutType { get; set; } = "cut";
    public float SpeedSeconds { get; set; } = 0.3f;
    public string Easing { get; set; } = "Linear";
}
