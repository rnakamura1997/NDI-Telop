using NdiTelop.Services;
using Xunit;

namespace NdiTelop.Tests.Services;

public class SettingsServiceTests : IDisposable
{
    private readonly string _testDataDir;
    private readonly string _settingsPath;
    private readonly string _exportPath;

    public SettingsServiceTests()
    {
        _testDataDir = Path.Combine(Path.GetTempPath(), "NdiTelopSettingsTests", Guid.NewGuid().ToString("N"));
        _settingsPath = Path.Combine(_testDataDir, "data", "appsettings.json");
        _exportPath = Path.Combine(_testDataDir, "exports", "appsettings-export.json");
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDataDir))
        {
            Directory.Delete(_testDataDir, true);
        }
    }

    [Fact]
    public async Task SaveAsync_AndLoadAsync_ShouldPersistNdiConfig()
    {
        var service = new SettingsService(_settingsPath);
        service.Settings.Ndi.SourceName = "Persisted Source";
        service.Settings.Ndi.ResolutionWidth = 1280;
        service.Settings.Ndi.ResolutionHeight = 720;

        await service.SaveAsync();

        var loaded = new SettingsService(_settingsPath);
        await loaded.LoadAsync();

        Assert.Equal("Persisted Source", loaded.Settings.Ndi.SourceName);
        Assert.Equal(1280, loaded.Settings.Ndi.ResolutionWidth);
        Assert.Equal(720, loaded.Settings.Ndi.ResolutionHeight);
    }

    [Fact]
    public async Task ExportImport_ShouldRoundTripSettings()
    {
        var service = new SettingsService(_settingsPath);
        service.Settings.Ndi.SourceName = "Exported Source";
        service.Settings.HttpPort = 9090;

        await service.ExportAsync(_exportPath);

        var imported = new SettingsService(_settingsPath);
        await imported.ImportAsync(_exportPath);

        Assert.Equal("Exported Source", imported.Settings.Ndi.SourceName);
        Assert.Equal(9090, imported.Settings.HttpPort);
        Assert.True(File.Exists(_settingsPath));
    }
    [Fact]
    public void Constructor_ShouldUseDefaultExternalControlPorts()
    {
        var service = new SettingsService(_settingsPath);

        Assert.Equal(5000, service.Settings.WebApiPort);
        Assert.Equal(8000, service.Settings.OscPort);
    }

}
