using NdiTelop.Interfaces;
using NdiTelop.Models;
using SkiaSharp;

namespace NdiTelop.Services;

public class RenderService : IRenderService
{
    public SKBitmap Render(Preset preset, int width, int height) => new(width, height);

    public SKBitmap RenderTransition(Preset from, Preset to, float progress, AnimationConfig config)
        => new(1920, 1080);
}
