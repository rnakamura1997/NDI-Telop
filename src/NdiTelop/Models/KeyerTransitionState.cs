namespace NdiTelop.Models;

public class KeyerTransitionState
{
    public KeyerDestination Destination { get; init; }
    public bool FromKeyOn { get; init; }
    public bool ToKeyOn { get; init; }
    public float Progress { get; set; }
    public AnimationConfig Config { get; init; } = new();

    public bool IsActive => Progress < 1f;

    public float GetVisibilityProgress()
    {
        var clamped = Math.Clamp(Progress, 0f, 1f);
        return FromKeyOn == ToKeyOn
            ? (ToKeyOn ? 1f : 0f)
            : ToKeyOn
                ? clamped
                : 1f - clamped;
    }

    public string GetTransitionType()
        => ToKeyOn ? Config.InType : Config.OutType;
}
