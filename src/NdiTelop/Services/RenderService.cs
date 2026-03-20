using NdiTelop.Interfaces;
using NdiTelop.Models;
using SkiaSharp;
using System.IO;
using Serilog;

namespace NdiTelop.Services;

public class RenderService : IRenderService
{
    private readonly AssetService _assetService = new();
    private readonly ISettingsService? _settingsService;

    public RenderService(ISettingsService? settingsService = null)
    {
        _settingsService = settingsService;
    }

    public SKBitmap Render(Preset preset, int width, int height)
        => Render(preset, width, height, null);

    public SKBitmap Render(Preset preset, int width, int height, KeyerDestination? soloKeyerDestination)
    {
        var bitmap = new SKBitmap(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);

        DrawBackground(canvas, preset.Background, width, height);
        preset.EnsureTextBlocksInitialized();

        foreach (var keyer in GetRenderOrderedKeyers(preset, KeyerBusType.Usk, soloKeyerDestination))
        {
            DrawKeyer(canvas, keyer, width, height);
        }

        foreach (var keyer in GetRenderOrderedKeyers(preset, KeyerBusType.Dsk, soloKeyerDestination))
        {
            DrawKeyer(canvas, keyer, width, height);
        }

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

    private void DrawBackground(SKCanvas canvas, BackgroundStyle bg, int width, int height)
    {
        if (string.Equals(bg.Type, "image", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(bg.AssetPath))
        {
            var backgroundPath = ResolveAssetPath(bg.AssetPath);
            if (File.Exists(backgroundPath))
            {
                using var image = SKBitmap.Decode(backgroundPath);
                if (image != null)
                {
                    canvas.DrawBitmap(image, new SKRect(0, 0, width, height));
                    return;
                }
            }
        }

        if (string.Equals(bg.Type, "transparent", StringComparison.OrdinalIgnoreCase)) return;

        var c = SKColor.Parse(bg.Color).WithAlpha((byte)(Math.Clamp(bg.Alpha, 0f, 1f) * 255));
        using var paint = new SKPaint { Color = c };
        canvas.DrawRect(0, 0, width, height, paint);
    }

    private void DrawKeyer(SKCanvas canvas, KeyerSlot keyer, int width, int height)
    {
        if (!keyer.KeyOn)
        {
            return;
        }

        var opacity = Math.Clamp(keyer.Opacity, 0.0, 1.0);
        if (opacity <= 0)
        {
            return;
        }

        using var layerPaint = new SKPaint { Color = SKColors.White.WithAlpha((byte)(opacity * 255)) };
        canvas.SaveLayer(layerPaint);
        DrawTextBlocks(canvas, keyer.TextBlocks, width, height);
        DrawOverlays(canvas, keyer.Overlays, width, height);
        canvas.Restore();
    }

    private static IEnumerable<KeyerSlot> GetRenderOrderedKeyers(Preset preset, KeyerBusType busType, KeyerDestination? soloKeyerDestination)
        => preset.Keyers
            .Where(keyer => keyer.BusType == busType)
            .Where(keyer => !soloKeyerDestination.HasValue || keyer.Destination == soloKeyerDestination.Value)
            .OrderBy(keyer => keyer.Priority)
            .ThenBy(keyer => keyer.Destination);

    private void DrawOverlays(SKCanvas canvas, IReadOnlyList<OverlayItem> overlays, int width, int height)
    {
        foreach (var overlay in overlays)
        {
            if (!overlay.IsVisible || string.IsNullOrEmpty(overlay.Path)) continue;

            var resolvedPath = ResolveAssetPath(overlay.Path);
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

                var (drawWidth, drawHeight) = ResolveOverlaySize(overlay, image.Width, image.Height);
                var destRect = new SKRect(overlay.X, overlay.Y, overlay.X + drawWidth, overlay.Y + drawHeight);
                canvas.DrawBitmap(image, destRect, paint);
            }
        }
    }

    private static (int Width, int Height) ResolveOverlaySize(OverlayItem overlay, int sourceWidth, int sourceHeight)
    {
        var width = overlay.Width;
        var height = overlay.Height;

        if (width > 0 && height > 0)
        {
            return (width, height);
        }

        if (width > 0)
        {
            var scaledHeight = (int)Math.Round(width * (sourceHeight / (double)Math.Max(1, sourceWidth)));
            return (width, Math.Max(1, scaledHeight));
        }

        if (height > 0)
        {
            var scaledWidth = (int)Math.Round(height * (sourceWidth / (double)Math.Max(1, sourceHeight)));
            return (Math.Max(1, scaledWidth), height);
        }

        return (Math.Max(1, sourceWidth), Math.Max(1, sourceHeight));
    }

    private static void DrawTextBlocks(SKCanvas canvas, IReadOnlyList<TextBlock> blocks, int width, int height)
    {
        foreach (var block in blocks)
        {
            DrawTextLines(canvas, block.TextLines, block.TextStyle, block.TextLayout, width, height);
        }
    }

    private static void DrawTextLines(SKCanvas canvas, IReadOnlyList<TextLine> lines, TextStyleSettings? style, TextLayoutSettings? layout, int width, int height)
    {
        if (lines.Count == 0)
        {
            return;
        }

        const float lineSpacing = 10f;
        var measuredLines = new List<MeasuredTextLine>(lines.Count);

        foreach (var line in lines)
        {
            var fontFamily = GetEffectiveFontFamily(line, style);
            var fontSize = GetEffectiveFontSize(line, style);
            using var typeface = SKTypeface.FromFamilyName(fontFamily) ?? SKTypeface.Default;
            using var font = new SKFont(typeface, fontSize);
            font.MeasureText(line.Text, out var textBounds);

            measuredLines.Add(new MeasuredTextLine(line, fontFamily, fontSize, textBounds));
        }

        var totalTextHeight = measuredLines.Sum(line => line.Bounds.Height) + lineSpacing * Math.Max(0, measuredLines.Count - 1);
        var blockTop = GetAlignedTop(height, totalTextHeight, layout) + (layout?.OffsetY ?? 0f);
        var currentTop = blockTop;

        foreach (var measuredLine in measuredLines)
        {
            var fillColor = ParseColorOrDefault(GetEffectiveColor(measuredLine.Line, style), SKColors.White);
            var outlineThickness = Math.Max(0f, style?.OutlineThickness ?? 0f);
            var outlineColor = ParseColorOrDefault(style?.OutlineColor, SKColors.Black);
            var shadowOffsetX = style?.ShadowOffsetX ?? 0f;
            var shadowOffsetY = style?.ShadowOffsetY ?? 0f;
            var shadowBlur = Math.Max(0f, style?.ShadowBlur ?? 0f);
            var shadowColor = ParseColorOrDefault(style?.ShadowColor, SKColors.Transparent);

            using var typeface = SKTypeface.FromFamilyName(measuredLine.FontFamily) ?? SKTypeface.Default;
            using var font = new SKFont(typeface, measuredLine.FontSize);

            var x = GetAlignedX(width, measuredLine.Bounds, layout) + (layout?.OffsetX ?? 0f);
            var textBaseline = currentTop - measuredLine.Bounds.Top;

            if (shadowColor.Alpha > 0 && (shadowBlur > 0f || shadowOffsetX != 0f || shadowOffsetY != 0f))
            {
                using var shadowPaint = new SKPaint
                {
                    Color = shadowColor,
                    IsAntialias = true,
                    MaskFilter = shadowBlur > 0f ? SKMaskFilter.CreateBlur(SKBlurStyle.Normal, shadowBlur) : null
                };
                canvas.DrawText(measuredLine.Line.Text, x + shadowOffsetX, textBaseline + shadowOffsetY, font, shadowPaint);
            }

            if (outlineThickness > 0f)
            {
                using var outlinePaint = new SKPaint
                {
                    Color = outlineColor,
                    IsAntialias = true,
                    Style = SKPaintStyle.Stroke,
                    StrokeWidth = outlineThickness,
                    StrokeJoin = SKStrokeJoin.Round
                };
                canvas.DrawText(measuredLine.Line.Text, x, textBaseline, font, outlinePaint);
            }

            using var fillPaint = new SKPaint
            {
                Color = fillColor,
                IsAntialias = true,
                Style = SKPaintStyle.Fill
            };
            canvas.DrawText(measuredLine.Line.Text, x, textBaseline, font, fillPaint);
            currentTop += measuredLine.Bounds.Height + lineSpacing;
        }
    }

    private static float GetAlignedTop(int height, float totalTextHeight, TextLayoutSettings? layout)
        => (layout?.VerticalAlignment ?? VerticalTextAlignment.Center) switch
        {
            VerticalTextAlignment.Top => 0f,
            VerticalTextAlignment.Bottom => height - totalTextHeight,
            _ => (height - totalTextHeight) / 2f
        };

    private static float GetAlignedX(int width, SKRect textBounds, TextLayoutSettings? layout)
        => (layout?.HorizontalAlignment ?? HorizontalTextAlignment.Center) switch
        {
            HorizontalTextAlignment.Left => -textBounds.Left,
            HorizontalTextAlignment.Right => width - textBounds.Right,
            _ => (width - textBounds.Width) / 2f - textBounds.Left
        };

    private readonly record struct MeasuredTextLine(TextLine Line, string FontFamily, float FontSize, SKRect Bounds);

    private static string GetEffectiveFontFamily(TextLine line, TextStyleSettings? style)
        => !string.IsNullOrWhiteSpace(style?.FontFamily) ? style.FontFamily : line.FontFamily;

    private static float GetEffectiveFontSize(TextLine line, TextStyleSettings? style)
        => Math.Clamp(style?.FontSize > 0 ? style.FontSize : line.FontSize, 8, 300);

    private static string GetEffectiveColor(TextLine line, TextStyleSettings? style)
        => !string.IsNullOrWhiteSpace(style?.Color) ? style.Color : line.Color;

    private static SKColor ParseColorOrDefault(string? value, SKColor fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        return SKColor.TryParse(value, out var parsed) ? parsed : fallback;
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

    private string ResolveAssetPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        if (Path.IsPathRooted(path))
        {
            return path;
        }

        var configuredAssetPath = _settingsService?.Settings.AssetPath;
        if (!string.IsNullOrWhiteSpace(configuredAssetPath))
        {
            return Path.Combine(configuredAssetPath, path);
        }

        return _assetService.ResolvePath(path);
    }
}
