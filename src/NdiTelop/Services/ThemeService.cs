using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using NdiTelop.Models;
using System.Linq;

namespace NdiTelop.Services;

public class ThemeService
{
    public void ApplyTheme(ThemeSettings settings)
    {
        if (Application.Current == null)
        {
            return;
        }

        var mode = ParseThemeMode(settings.Mode);
        Application.Current.RequestedThemeVariant = mode;

        var accent = ParseColor(settings.AccentColor);
        settings.AccentColor = accent.ToString();

        if (Application.Current.Styles.FirstOrDefault(s => s is FluentTheme) is not FluentTheme fluentTheme)
        {
            return;
        }

        var lightPalette = GetOrCreatePalette(fluentTheme, ThemeVariant.Light);
        lightPalette.Accent = accent;

        var darkPalette = GetOrCreatePalette(fluentTheme, ThemeVariant.Dark);
        darkPalette.Accent = accent;
    }

    private static ThemeVariant ParseThemeMode(string? mode)
    {
        return mode?.Trim().ToLowerInvariant() switch
        {
            "dark" => ThemeVariant.Dark,
            _ => ThemeVariant.Light
        };
    }

    private static Color ParseColor(string? colorText)
    {
        if (!string.IsNullOrWhiteSpace(colorText) && Color.TryParse(colorText, out var color))
        {
            return color;
        }

        return Color.Parse("#FF0A84FF");
    }

    private static ColorPaletteResources GetOrCreatePalette(FluentTheme theme, ThemeVariant variant)
    {
        if (theme.Palettes.TryGetValue(variant, out var existing))
        {
            return existing;
        }

        var palette = new ColorPaletteResources();
        theme.Palettes[variant] = palette;
        return palette;
    }
}
