using NdiTelop.Interfaces;
using NdiTelop.Models;
using SkiaSharp;
using System.IO;
using Serilog;

namespace NdiTelop.Services;

public class RenderService : IRenderService
{
    private readonly AssetService _assetService = new();

    public SKBitmap Render(Preset preset, int width, int height)
    {
        var bitmap = new SKBitmap(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);

        DrawBackground(canvas, preset.Background, width, height);
        DrawTextLines(canvas, preset.TextLines, width, height);
        DrawOverlays(canvas, preset.Overlays, width, height);

        return bitmap;
    }

    public SKBitmap RenderTransition(Preset from, Preset to, float progress, AnimationConfig config, NdiConfig ndiConfig)
    {
        var p = Clamp01(progress);

        // トランジションのレンダリング解像度は NDI Config に従う
        var renderWidth = ndiConfig.ResolutionWidth;
        var renderHeight = ndiConfig.ResolutionHeight;

        using var fromBitmap = Render(from, renderWidth, renderHeight);
        using var toBitmap = Render(to, renderWidth, renderHeight);

        var output = new SKBitmap(renderWidth, renderHeight, SKColorType.Bgra8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(output);
        canvas.Clear(SKColors.Transparent);

        var transitionType = config.InType?.Trim() ?? "fade";

        if (string.Equals(transitionType, "slide", StringComparison.OrdinalIgnoreCase))
        {
            var x = (float)renderWidth * (1f - p);
            canvas.DrawBitmap(fromBitmap, 0, 0);
            canvas.DrawBitmap(toBitmap, x, 0);
            return output;
        }

        if (IsWipeTransition(transitionType, out var isVertical))
        {
            canvas.DrawBitmap(fromBitmap, 0, 0);

            if (isVertical)
            {
                var wipeHeight = renderHeight * p;
                if (wipeHeight > 0)
                {
                    var sourceRect = new SKRect(0, 0, renderWidth, wipeHeight);
                    var destRect = new SKRect(0, 0, renderWidth, wipeHeight);
                    canvas.DrawBitmap(toBitmap, sourceRect, destRect);
                }
            }
            else
            {
                var wipeWidth = renderWidth * p;
                if (wipeWidth > 0)
                {
                    var sourceRect = new SKRect(0, 0, wipeWidth, renderHeight);
                    var destRect = new SKRect(0, 0, wipeWidth, renderHeight);
                    canvas.DrawBitmap(toBitmap, sourceRect, destRect);
                }
            }

            return output;
        }

        if (string.Equals(transitionType, "zoom", StringComparison.OrdinalIgnoreCase))
        {
            canvas.DrawBitmap(fromBitmap, 0, 0);

            var easedProgress = Math.Max(0.05f, p);
            var scaledWidth = renderWidth * easedProgress;
            var scaledHeight = renderHeight * easedProgress;
            var left = (renderWidth - scaledWidth) / 2f;
            var top = (renderHeight - scaledHeight) / 2f;

            using var zoomPaint = new SKPaint { Color = new SKColor(255, 255, 255, (byte)(255 * p)), IsAntialias = true };
            canvas.DrawBitmap(toBitmap, new SKRect(left, top, left + scaledWidth, top + scaledHeight), zoomPaint);
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

    private void DrawOverlays(SKCanvas canvas, IReadOnlyList<OverlayItem> overlays, int width, int height)
    {
        foreach (var overlay in overlays)
        {
            if (!overlay.IsVisible || string.IsNullOrEmpty(overlay.Path)) continue;

            var resolvedPath = _assetService.ResolvePath(overlay.Path);
            if (string.IsNullOrWhiteSpace(resolvedPath) || !File.Exists(resolvedPath))
            {
                Log.Warning("Overlay asset not found and skipped. Path={Path}", overlay.Path);
                continue;
            }

            SKBitmap? image = null;
            try
            {
                image = SKBitmap.Decode(resolvedPath);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Overlay asset decode failed. Path={Path}", resolvedPath);
            }

            using (image)
            {
                if (image == null)
                {
                    Log.Warning("Overlay decode returned null. Path={Path}", resolvedPath);
                    continue;
                }

            var opacity = Math.Clamp(overlay.Opacity, 0.0, 1.0);
                if (opacity <= 0) continue;

                using var paint = new SKPaint
                {
                    Color = SKColors.White.WithAlpha((byte)(opacity * 255)),
                    BlendMode = SKBlendMode.SrcOver,
                    IsAntialias = true
                };

                var drawWidth = overlay.Width > 0 ? overlay.Width : image.Width;
                var drawHeight = overlay.Height > 0 ? overlay.Height : image.Height;
                var destRect = new SKRect(overlay.X, overlay.Y, overlay.X + drawWidth, overlay.Y + drawHeight);
                canvas.DrawBitmap(image, destRect, paint);
            }
        }
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

    private static bool IsWipeTransition(string transitionType, out bool isVertical)
    {
        isVertical = false;
        if (string.Equals(transitionType, "wipe", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(transitionType, "wipe-horizontal", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(transitionType, "wipe-left-right", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(transitionType, "wipe-vertical", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(transitionType, "wipe-up-down", StringComparison.OrdinalIgnoreCase))
        {
            isVertical = true;
            return true;
        }

        return false;
    }
}
