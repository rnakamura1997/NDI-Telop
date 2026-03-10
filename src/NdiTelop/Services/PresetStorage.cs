using System.Text.Json;
using NdiTelop.Models;

namespace NdiTelop.Services;

public class PresetStorage
{
    private readonly JsonSerializerOptions _options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public async Task<List<Preset>> LoadFromFileAsync(string filePath)
    {
        if (!File.Exists(filePath)) return [];

        try
        {
            await using var stream = File.OpenRead(filePath);
            var presets = await JsonSerializer.DeserializeAsync<List<Preset>>(stream, _options);
            return presets ?? [];
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading presets from {filePath}: {ex.Message}");
            return [];
        }
    }

    public async Task SaveToFileAsync(string filePath, IReadOnlyList<Preset> presets)
    {
        try
        {
            var directory = Path.GetDirectoryName(filePath);
            if (directory != null && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            await using var stream = File.Create(filePath);
            await JsonSerializer.SerializeAsync(stream, presets, _options);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving presets to {filePath}: {ex.Message}");
        }
    }
}
