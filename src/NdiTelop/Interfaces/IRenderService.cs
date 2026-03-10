using NdiTelop.Models;
using SkiaSharp;

namespace NdiTelop.Interfaces;

public interface IRenderService
{
    SKBitmap Render(Preset preset, int width, int height);
    SKBitmap RenderTransition(Preset from, Preset to, float progress, AnimationConfig config, NdiConfig ndiConfig);
}
