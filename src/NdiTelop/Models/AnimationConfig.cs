namespace NdiTelop.Models;
public class AnimationConfig
{
    public string InType { get; set; } = "fade";
    public string OutType { get; set; } = "fade";
    public float SpeedSeconds { get; set; } = 0.3f;
    public string Easing { get; set; } = "Linear";
}
