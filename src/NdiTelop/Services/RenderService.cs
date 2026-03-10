using NdiTelop.Interfaces;
using NdiTelop.Models;
using SkiaSharp;

namespace NdiTelop.Services;

public class RenderService : IRenderService
{
    public SKBitmap Render(Preset preset, int width, int height)
    {
        var bitmap = new SKBitmap(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);
        DrawBackground(canvas, preset.Background, width, height);
        DrawTextLines(canvas, preset.TextLines, width, height);
        return bitmap;
    }

    public SKBitmap RenderTransition(Preset from, Preset to, float progress, AnimationConfig config)
    {
        var p = RenderTransitionHelper.Clamp01(progress);
        using var fromBitmap = Render(from, 1920, 1080);
        using var toBitmap = Render(to, 1920, 1080);

        var output = new SKBitmap(1920, 1080, SKColorType.Bgra8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(output);
        canvas.Clear(SKColors.Transparent);

        if (string.Equals(config.InType, "slide", StringComparison.OrdinalIgnoreCase))
        {
            var x = 1920f * (1f - p);
            canvas.DrawBitmap(fromBitmap, 0, 0);
            canvas.DrawBitmap(toBitmap, x, 0);
            return output;
        }

        using var fromPaint = new SKPaint { Color = new SKColor(255, 255, 255, (byte)(255 * (1f - p))) };
        using var toPaint = new SKPaint { Color = new SKColor(255, 255, 255, (byte)(255 * p)) };
        canvas.DrawBitmap(fromBitmap, 0, 0, fromPaint);
        canvas.DrawBitmap(toBitmap, 0, 0, toPaint);
        return output;
    }

    private static void DrawBackground(SKCanvas canvas, BackgroundStyle bg, int width, int height)
    {
        if (string.Equals(bg.Type, "transparent", StringComparison.OrdinalIgnoreCase)) return;
        var c = SKColor.Parse(bg.Color).WithAlpha((byte)(Math.Clamp(bg.Alpha, 0f, 1f) * 255));
        using var paint = new SKPaint { Color = c };
        canvas.DrawRect(0, 0, width, height, paint);
    }

    private static void DrawTextLines(SKCanvas canvas, IReadOnlyList<TextLine> lines, int width, int height)
    {
        var y = height / 2f;
        foreach (var line in lines)
        {
            using var paint = new SKPaint
            {
                Color = SKColor.Parse(line.Color),
                IsAntialias = true,
                TextSize = Math.Clamp(line.FontSize, 8, 300),
                Typeface = SKTypeface.FromFamilyName(line.FontFamily)
            };
            var textWidth = paint.MeasureText(line.Text);
            canvas.DrawText(line.Text, (width - textWidth) / 2f, y, paint);
            y += paint.TextSize + 10;
        }
    }
}
