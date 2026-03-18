using System.IO.Compression;
using System.Text.Json;
using NdiTelop.Models;
using Serilog;

namespace NdiTelop.Services;

public class BackupArchiveService
{
    private const string SettingsEntryName = "settings/appsettings.json";
    private const string PresetsEntryName = "presets/presets.json";
    private const string ManifestEntryName = "manifest.json";

    private readonly SettingsService _settingsService;
    private readonly PresetService _presetService;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public BackupArchiveService(SettingsService settingsService, PresetService presetService)
    {
        _settingsService = settingsService;
        _presetService = presetService;
    }

    public async Task CreateBackupAsync(string archivePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(archivePath);

        await _settingsService.SaveAsync();
        await _presetService.SavePresetsSnapshotAsync();

        var directory = Path.GetDirectoryName(archivePath);
        if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        if (File.Exists(archivePath))
        {
            File.Delete(archivePath);
        }

        using var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create);
        archive.CreateEntryFromFile(_settingsService.SettingsFilePath, SettingsEntryName);
        archive.CreateEntryFromFile(_presetService.UserPresetPath, PresetsEntryName);

        var manifest = new BackupManifest
        {
            SchemaVersion = "1.0",
            CreatedAtUtc = DateTimeOffset.UtcNow,
            AssetPath = _settingsService.Settings.AssetPath,
            AssetCount = AddAssetEntries(archive, _settingsService.Settings.AssetPath)
        };

        var manifestEntry = archive.CreateEntry(ManifestEntryName);
        await using var manifestStream = manifestEntry.Open();
        await JsonSerializer.SerializeAsync(manifestStream, manifest, _jsonOptions);

        Log.Information("Backup archive created. Path={ArchivePath}, AssetCount={AssetCount}", archivePath, manifest.AssetCount);
    }

    public async Task RestoreBackupAsync(string archivePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(archivePath);
        if (!File.Exists(archivePath))
        {
            throw new FileNotFoundException("Backup archive was not found.", archivePath);
        }

        using var archive = ZipFile.OpenRead(archivePath);
        var settingsEntry = archive.GetEntry(SettingsEntryName)
            ?? throw new InvalidDataException("Backup archive does not contain application settings.");
        var presetsEntry = archive.GetEntry(PresetsEntryName)
            ?? throw new InvalidDataException("Backup archive does not contain presets.");

        await using (var settingsEntryStream = settingsEntry.Open())
        {
            var restoredSettings = await JsonSerializer.DeserializeAsync<AppSettings>(settingsEntryStream, _jsonOptions)
                ?? throw new InvalidDataException("Backup settings file is invalid.");

            Directory.CreateDirectory(Path.GetDirectoryName(_settingsService.SettingsFilePath)!);
            await using var settingsFile = File.Create(_settingsService.SettingsFilePath);
            await JsonSerializer.SerializeAsync(settingsFile, restoredSettings, _jsonOptions);
        }

        await _settingsService.LoadAsync();

        Directory.CreateDirectory(Path.GetDirectoryName(_presetService.UserPresetPath)!);
        await using (var presetInput = presetsEntry.Open())
        await using (var presetOutput = File.Create(_presetService.UserPresetPath))
        {
            await presetInput.CopyToAsync(presetOutput);
        }

        await RestoreAssetsAsync(archive, _settingsService.Settings.AssetPath);
        await _presetService.LoadPresetsAsync();

        Log.Information("Backup archive restored. Path={ArchivePath}, AssetPath={AssetPath}", archivePath, _settingsService.Settings.AssetPath);
    }

    private static int AddAssetEntries(ZipArchive archive, string assetDirectory)
    {
        if (string.IsNullOrWhiteSpace(assetDirectory) || !Directory.Exists(assetDirectory))
        {
            return 0;
        }

        var count = 0;
        foreach (var assetPath in Directory.EnumerateFiles(assetDirectory, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(assetDirectory, assetPath).Replace('\\', '/');
            if (relativePath.StartsWith(".thumbs/", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            archive.CreateEntryFromFile(assetPath, $"assets/{relativePath}");
            count++;
        }

        return count;
    }

    private static async Task RestoreAssetsAsync(ZipArchive archive, string assetDirectory)
    {
        if (string.IsNullOrWhiteSpace(assetDirectory))
        {
            return;
        }

        var fullAssetDirectory = Path.GetFullPath(assetDirectory);
        if (Path.GetPathRoot(fullAssetDirectory) == fullAssetDirectory)
        {
            throw new InvalidOperationException("Refusing to restore assets into a filesystem root directory.");
        }

        if (Directory.Exists(fullAssetDirectory))
        {
            Directory.Delete(fullAssetDirectory, recursive: true);
        }

        Directory.CreateDirectory(fullAssetDirectory);

        foreach (var entry in archive.Entries.Where(static e => e.FullName.StartsWith("assets/", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(e.Name)))
        {
            var relativePath = entry.FullName["assets/".Length..];
            var destinationPath = Path.Combine(fullAssetDirectory, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);

            await using var input = entry.Open();
            await using var output = File.Create(destinationPath);
            await input.CopyToAsync(output);
        }
    }

    private sealed class BackupManifest
    {
        public string SchemaVersion { get; set; } = "1.0";
        public DateTimeOffset CreatedAtUtc { get; set; }
        public string AssetPath { get; set; } = string.Empty;
        public int AssetCount { get; set; }
    }
}
