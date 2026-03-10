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
        var p = Clamp01(progress);

        using var fromBitmap = Render(from, 1920, 1080);
        using var toBitmap = Render(to, 1920, 1080);

        var output = new SKBitmap(1920, 1080, SKColorType.Bgra8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(output);
        canvas.Clear(SKColors.Transparent);

        // Basic slide transition (left to right)
        if (string.Equals(config.InType, "slide", StringComparison.OrdinalIgnoreCase))
        {
            var x = 1920f * (1f - p);
            canvas.DrawBitmap(fromBitmap, 0, 0);
            canvas.DrawBitmap(toBitmap, x, 0);
            return output;
        }

        // Basic fade transition
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
        // Simple vertical centering for now
        var totalTextHeight = lines.Sum(line => line.FontSize + 10); // Estimate with line spacing
        var y = (height - totalTextHeight) / 2f;

        foreach (var line in lines)
        {
            using var font = new SKFont(SKTypeface.FromFamilyName(line.FontFamily), Math.Clamp((float)line.FontSize, 8, 300));
            using var paint = new SKPaint
            {
                Color = SKColor.Parse(line.Color),
                IsAntialias = true
            };

            var textBounds = new SKRect();
            font.MeasureText(line.Text, out textBounds);
            var x = (width - textBounds.Width) / 2 - textBounds.Left;
            var textBaseline = y + font.Size;
            canvas.DrawText(line.Text, x, textBaseline, font, paint);
            y += font.Size + 10; // Move to next line
        }
    }

    private static float Clamp01(float value)
    {
        if (value < 0) return 0;
        if (value > 1) return 1;
        return value;
    }
}
