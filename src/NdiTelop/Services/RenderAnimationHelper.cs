using NdiTelop.Models;
using SkiaSharp;

namespace NdiTelop.Services;

/// <summary>
/// フェーズ1で使用するトランジション補助ロジック。
/// </summary>
public static class RenderAnimationHelper
{
    public static SKPoint GetSlideOffset(string animationType, float progress, int width, int height)
    {
        var clamped = Math.Clamp(progress, 0f, 1f);
        return animationType.ToLowerInvariant() switch
        {
            "slide-up" => new SKPoint(0, height * (1f - clamped)),
            "slide-down" => new SKPoint(0, -height * (1f - clamped)),
            "slide-left" => new SKPoint(width * (1f - clamped), 0),
            "slide-right" => new SKPoint(-width * (1f - clamped), 0),
            _ => SKPoint.Empty,
        };
    }

    public static byte GetFadeAlpha(float progress)
    {
        var clamped = Math.Clamp(progress, 0f, 1f);
        return (byte)(255f * clamped);
    }
}
