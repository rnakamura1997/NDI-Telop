using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using NdiTelop.Models;

namespace NdiTelop.Services;

public sealed class ExternalDataSourceService
{
    private static readonly Regex PlaceholderPattern = new(@"{{\s*(?<key>[a-zA-Z0-9_\-.]+)\s*}}", RegexOptions.Compiled);
    private readonly HttpClient _httpClient;

    public ExternalDataSourceService(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient();
    }

    public async Task<IReadOnlyDictionary<string, string>> LoadAsync(string source, CancellationToken cancellationToken)
    {
        var raw = await ReadSourceAsync(source, cancellationToken);
        return ParseSource(source, raw);
    }

    public string ApplyTemplate(string? template, IReadOnlyDictionary<string, string>? values)
    {
        if (string.IsNullOrEmpty(template) || values == null || values.Count == 0)
        {
            return template ?? string.Empty;
        }

        return PlaceholderPattern.Replace(template, match =>
        {
            var key = match.Groups["key"].Value;
            return values.TryGetValue(key, out var value) ? value : match.Value;
        });
    }

    private async Task<string> ReadSourceAsync(string source, CancellationToken cancellationToken)
    {
        if (Uri.TryCreate(source, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            return await _httpClient.GetStringAsync(uri, cancellationToken);
        }

        return await File.ReadAllTextAsync(source, cancellationToken);
    }

    private static IReadOnlyDictionary<string, string> ParseSource(string source, string raw)
    {
        var extension = Path.GetExtension(source);
        if (string.Equals(extension, ".csv", StringComparison.OrdinalIgnoreCase))
        {
            return ParseCsv(raw);
        }

        return ParseJson(raw);
    }

    private static IReadOnlyDictionary<string, string> ParseJson(string raw)
    {
        using var document = JsonDocument.Parse(raw);
        JsonElement element = document.RootElement;
        if (element.ValueKind == JsonValueKind.Array)
        {
            element = element.EnumerateArray().FirstOrDefault();
            if (element.ValueKind == JsonValueKind.Undefined)
            {
                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }
        }

        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        FlattenJson(element, values, null);
        return values;
    }

    private static void FlattenJson(JsonElement element, IDictionary<string, string> values, string? prefix)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    var childPrefix = string.IsNullOrWhiteSpace(prefix) ? property.Name : $"{prefix}.{property.Name}";
                    FlattenJson(property.Value, values, childPrefix);
                }
                break;
            case JsonValueKind.Array:
                var index = 0;
                foreach (var item in element.EnumerateArray())
                {
                    FlattenJson(item, values, $"{prefix}[{index}]");
                    index++;
                }
                break;
            default:
                if (!string.IsNullOrWhiteSpace(prefix))
                {
                    values[prefix] = element.ValueKind switch
                    {
                        JsonValueKind.String => element.GetString() ?? string.Empty,
                        JsonValueKind.Number => element.GetRawText(),
                        JsonValueKind.True => bool.TrueString,
                        JsonValueKind.False => bool.FalseString,
                        JsonValueKind.Null => string.Empty,
                        _ => element.GetRawText()
                    };
                }
                break;
        }
    }

    private static IReadOnlyDictionary<string, string> ParseCsv(string raw)
    {
        var lines = raw.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (lines.Length < 2)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        var headers = ParseCsvLine(lines[0]);
        var firstRow = ParseCsvLine(lines[1]);
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < headers.Count; i++)
        {
            var key = headers[i];
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            values[key] = i < firstRow.Count ? firstRow[i] : string.Empty;
        }

        return values;
    }

    private static List<string> ParseCsvLine(string line)
    {
        var values = new List<string>();
        var builder = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (ch == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    builder.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }

                continue;
            }

            if (ch == ',' && !inQuotes)
            {
                values.Add(builder.ToString());
                builder.Clear();
                continue;
            }

            builder.Append(ch);
        }

        values.Add(builder.ToString());
        return values;
    }
}
