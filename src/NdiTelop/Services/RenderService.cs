using NdiTelop.Interfaces;
using NdiTelop.Models;
using SkiaSharp;

namespace NdiTelop.Services;

/// <summary>
/// SkiaSharp によるテロップ描画サービス。
/// </summary>
public class RenderService : IRenderService
{
    public SKBitmap Render(Preset preset, int width, int height)
    {
        var bitmap = new SKBitmap(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);

        DrawBackground(canvas, preset, width, height);
        DrawTextLines(canvas, preset);

        return bitmap;
    }

    public SKBitmap RenderTransition(Preset from, Preset to, float progress, AnimationConfig config)
    {
        var fromFrame = Render(from, 1920, 1080);
        var toFrame = Render(to, 1920, 1080);

        var output = new SKBitmap(1920, 1080, SKColorType.Bgra8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(output);
        canvas.Clear(SKColors.Transparent);

        var clamped = Math.Clamp(progress, 0f, 1f);
        var transitionType = (config.InType ?? "cut").ToLowerInvariant();
        if (transitionType == "fade")
        {
            using var fromPaint = new SKPaint { Color = new SKColor(255, 255, 255, (byte)(255 * (1f - clamped))) };
            using var toPaint = new SKPaint { Color = new SKColor(255, 255, 255, RenderAnimationHelper.GetFadeAlpha(clamped)) };
            canvas.DrawBitmap(fromFrame, 0, 0, fromPaint);
            canvas.DrawBitmap(toFrame, 0, 0, toPaint);
        }
        else if (transitionType.StartsWith("slide-", StringComparison.Ordinal))
        {
            var offset = RenderAnimationHelper.GetSlideOffset(transitionType, clamped, output.Width, output.Height);
            canvas.DrawBitmap(fromFrame, 0, 0);
            canvas.DrawBitmap(toFrame, offset.X, offset.Y);
        }
        else
        {
            canvas.DrawBitmap(clamped < 1f ? fromFrame : toFrame, 0, 0);
        }

        fromFrame.Dispose();
        toFrame.Dispose();
        return output;
    }

    private static void DrawBackground(SKCanvas canvas, Preset preset, int width, int height)
    {
        if (string.Equals(preset.Background.Type, "transparent", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var color = SKColor.TryParse(preset.Background.Color, out var parsed)
            ? parsed.WithAlpha((byte)(Math.Clamp(preset.Background.Alpha, 0, 1) * 255))
            : new SKColor(0, 0, 0, 180);

        using var paint = new SKPaint { Color = color, IsAntialias = true };
        canvas.DrawRoundRect(SKRect.Create(0, 0, width, height), 8, 8, paint);
    }

    private static void DrawTextLines(SKCanvas canvas, Preset preset)
    {
        if (preset.TextLines.Count == 0)
        {
            return;
        }

        var y = Math.Max(80, preset.Y == 0 ? 120 : preset.Y);
        foreach (var line in preset.TextLines)
        {
            using var paint = CreateTextPaint(line);
            var x = ResolveX(line.Alignment, canvas.LocalClipBounds.Width, line.Text, paint, preset.X);
            canvas.DrawText(line.Text, x, y, paint);
            y += line.FontSize + 12;
        }
    }

    private static SKPaint CreateTextPaint(TextLine line)
    {
        var typeface = SKTypeface.FromFamilyName(
            line.FontFamily,
            line.Bold ? SKFontStyleWeight.Bold : SKFontStyleWeight.Normal,
            SKFontStyleWidth.Normal,
            line.Italic ? SKFontStyleSlant.Italic : SKFontStyleSlant.Upright);

        return new SKPaint
        {
            IsAntialias = true,
            Typeface = typeface,
            TextSize = Math.Clamp(line.FontSize, 8, 300),
            Color = SKColor.TryParse(line.Color, out var color) ? color : SKColors.White,
            StrokeWidth = Math.Clamp(line.OutlineWidth, 0, 20),
            Style = line.OutlineWidth > 0 ? SKPaintStyle.StrokeAndFill : SKPaintStyle.Fill
        };
    }

    private static float ResolveX(TextAlignmentType alignment, float width, string text, SKPaint paint, int xOffset)
    {
        var textWidth = paint.MeasureText(text);
        return alignment switch
        {
            TextAlignmentType.Left => 32 + xOffset,
            TextAlignmentType.Right => width - textWidth - 32 + xOffset,
            _ => (width - textWidth) / 2f + xOffset,
        };
    }
}
