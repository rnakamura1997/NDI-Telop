using NdiTelop.Models;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace NdiTelop.Services;

internal static class CsvPresetCodec
{
    // CSV policy:
    // - Export writes UTF-8 with BOM and always emits a header row.
    // - Import accepts files with or without header.
    // - Empty lines are ignored.
    // - Malformed rows (insufficient columns, parse errors, invalid JSON) are skipped.

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly string[] Header =
    [
        "Id",
        "Name",
        "AutoClearSeconds",
        "BackgroundType",
        "BackgroundColor",
        "BackgroundAlpha",
        "AnimationInType",
        "AnimationOutType",
        "AnimationSpeedSeconds",
        "AnimationEasing",
        "TextLinesJson",
        "OverlaysJson"
    ];

    public static async Task WriteAsync(string filePath, IReadOnlyList<Preset> presets)
    {
        await using var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
        await using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

        await writer.WriteLineAsync(ToCsvLine(Header));

        foreach (var preset in presets)
        {
            var row = new[]
            {
                preset.Id,
                preset.Name,
                preset.AutoClearSeconds.ToString(CultureInfo.InvariantCulture),
                preset.Background.Type,
                preset.Background.Color,
                preset.Background.Alpha.ToString(CultureInfo.InvariantCulture),
                preset.Animation.InType,
                preset.Animation.OutType,
                preset.Animation.SpeedSeconds.ToString(CultureInfo.InvariantCulture),
                preset.Animation.Easing,
                JsonSerializer.Serialize(preset.TextLines, JsonOptions),
                JsonSerializer.Serialize(preset.Overlays, JsonOptions)
            };

            await writer.WriteLineAsync(ToCsvLine(row));
        }
    }

    public static async Task<List<Preset>> ReadAsync(string filePath)
    {
        var content = await File.ReadAllTextAsync(filePath);
        if (string.IsNullOrWhiteSpace(content))
        {
            return [];
        }

        var lines = content
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n');

        var presets = new List<Preset>();
        var startIndex = 0;

        while (startIndex < lines.Length && string.IsNullOrWhiteSpace(lines[startIndex]))
        {
            startIndex++;
        }

        if (startIndex >= lines.Length)
        {
            return presets;
        }

        var firstTokens = ParseCsvLine(lines[startIndex]);
        if (IsHeader(firstTokens))
        {
            startIndex++;
        }

        for (var i = startIndex; i < lines.Length; i++)
        {
            var line = lines[i];
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var tokens = ParseCsvLine(line);
            if (tokens.Count == 0)
            {
                continue;
            }

            if (tokens.Count < Header.Length)
            {
                continue;
            }

            var preset = TryCreatePreset(tokens);
            if (preset is not null)
            {
                presets.Add(preset);
            }
        }

        return presets;
    }

    private static Preset? TryCreatePreset(IReadOnlyList<string> tokens)
    {
        try
        {
            var id = tokens[0].Trim();
            var name = tokens[1].Trim();
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            if (!int.TryParse(tokens[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var autoClearSeconds))
            {
                return null;
            }

            if (!double.TryParse(tokens[5], NumberStyles.Float, CultureInfo.InvariantCulture, out var backgroundAlpha))
            {
                return null;
            }

            if (!float.TryParse(tokens[8], NumberStyles.Float, CultureInfo.InvariantCulture, out var animationSpeedSeconds))
            {
                return null;
            }

            var textLines = JsonSerializer.Deserialize<List<TextLine>>(tokens[10], JsonOptions) ?? [];
            var overlays = JsonSerializer.Deserialize<List<OverlayItem>>(tokens[11], JsonOptions) ?? [];

            return new Preset
            {
                Id = id,
                Name = name,
                AutoClearSeconds = autoClearSeconds,
                TextLines = [.. textLines],
                Overlays = overlays,
                Background = new BackgroundStyle
                {
                    Type = tokens[3],
                    Color = tokens[4],
                    Alpha = backgroundAlpha
                },
                Animation = new AnimationConfig
                {
                    InType = tokens[6],
                    OutType = tokens[7],
                    SpeedSeconds = animationSpeedSeconds,
                    Easing = tokens[9]
                }
            };
        }
        catch
        {
            return null;
        }
    }

    private static bool IsHeader(IReadOnlyList<string> tokens)
    {
        if (tokens.Count < Header.Length)
        {
            return false;
        }

        for (var i = 0; i < Header.Length; i++)
        {
            if (!string.Equals(tokens[i], Header[i], StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private static string ToCsvLine(IReadOnlyList<string> values)
    {
        return string.Join(',', values.Select(EscapeCsvValue));
    }

    private static string EscapeCsvValue(string value)
    {
        if (value.Contains('"'))
        {
            value = value.Replace("\"", "\"\"");
        }

        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
        {
            return $"\"{value}\"";
        }

        return value;
    }

    private static List<string> ParseCsvLine(string line)
    {
        var values = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (c == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }

                continue;
            }

            if (c == ',' && !inQuotes)
            {
                values.Add(current.ToString());
                current.Clear();
                continue;
            }

            current.Append(c);
        }

        values.Add(current.ToString());
        return values;
    }
}
