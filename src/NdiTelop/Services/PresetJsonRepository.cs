using System.Text.Json;
using NdiTelop.Models;

namespace NdiTelop.Services;

/// <summary>
/// プリセット JSON 永続化レイヤー。
/// </summary>
public sealed class PresetJsonRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private readonly PresetStoragePaths _paths;

    public PresetJsonRepository(PresetStoragePaths paths)
    {
        _paths = paths;
    }

    public async Task<List<Preset>> LoadAllAsync(CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_paths.PresetDirectoryPath);

        var presetFiles = Directory.GetFiles(_paths.PresetDirectoryPath, "*.json", SearchOption.TopDirectoryOnly);
        if (presetFiles.Length == 0)
        {
            return await LoadDefaultAsync(cancellationToken);
        }

        var results = new List<Preset>();
        foreach (var file in presetFiles)
        {
            await using var stream = File.OpenRead(file);
            var preset = await JsonSerializer.DeserializeAsync<Preset>(stream, JsonOptions, cancellationToken);
            if (preset is not null)
            {
                results.Add(preset);
            }
        }

        return results;
    }

    public async Task SaveAsync(Preset preset, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_paths.PresetDirectoryPath);
        var filePath = Path.Combine(_paths.PresetDirectoryPath, $"{preset.Id}.json");
        await using var stream = File.Create(filePath);
        await JsonSerializer.SerializeAsync(stream, preset, JsonOptions, cancellationToken);
    }

    public Task DeleteAsync(string id)
    {
        var filePath = Path.Combine(_paths.PresetDirectoryPath, $"{id}.json");
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }

        return Task.CompletedTask;
    }

    private async Task<List<Preset>> LoadDefaultAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_paths.DefaultPresetsFilePath))
        {
            return [];
        }

        await using var stream = File.OpenRead(_paths.DefaultPresetsFilePath);
        var presets = await JsonSerializer.DeserializeAsync<List<Preset>>(stream, JsonOptions, cancellationToken);
        return presets ?? [];
    }
}
