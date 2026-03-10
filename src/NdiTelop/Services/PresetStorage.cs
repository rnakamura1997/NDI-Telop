using System.Text.Json;
using NdiTelop.Models;

namespace NdiTelop.Services;

public sealed class PresetStorage
{
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };

    public async Task<IReadOnlyList<Preset>> LoadFromFileAsync(string path)
    {
        if (!File.Exists(path)) return [];
        await using var stream = File.OpenRead(path);
        var presets = await JsonSerializer.DeserializeAsync<List<Preset>>(stream, _jsonOptions);
        return presets ?? [];
    }

    public async Task SaveToFileAsync(string path, IReadOnlyList<Preset> presets)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, presets, _jsonOptions);
    }
}
