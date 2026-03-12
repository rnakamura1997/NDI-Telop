using System.Text.Json;
using NdiTelop.Interfaces;
using NdiTelop.Models;

namespace NdiTelop.Services;

public class SettingsService : ISettingsService
{
    private readonly string _settingsFilePath;
    private readonly JsonSerializerOptions _options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public AppSettings Settings { get; private set; } = new();

    public SettingsService(string? settingsFilePath = null)
    {
        _settingsFilePath = settingsFilePath
            ?? Path.Combine(AppContext.BaseDirectory, "data", "appsettings.json");
    }

    public async Task LoadAsync()
    {
        if (!File.Exists(_settingsFilePath)) return;

        await using var stream = File.OpenRead(_settingsFilePath);
        var loaded = await JsonSerializer.DeserializeAsync<AppSettings>(stream, _options);
        if (loaded != null)
        {
            Settings = loaded;
        }
    }

    public async Task SaveAsync()
    {
        var directory = Path.GetDirectoryName(_settingsFilePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var stream = File.Create(_settingsFilePath);
        await JsonSerializer.SerializeAsync(stream, Settings, _options);
    }

    public async Task ExportAsync(string filePath)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var stream = File.Create(filePath);
        await JsonSerializer.SerializeAsync(stream, Settings, _options);
    }

    public async Task ImportAsync(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("Settings file was not found.", filePath);
        }

        await using var stream = File.OpenRead(filePath);
        var loaded = await JsonSerializer.DeserializeAsync<AppSettings>(stream, _options);
        if (loaded != null)
        {
            Settings = loaded;
            await SaveAsync();
        }
    }
}
