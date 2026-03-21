using NdiTelop.Models;
using NdiTelop.Services;
using NdiTelop.ViewModels;
using NSubstitute;
using NdiTelop.Interfaces;
using SkiaSharp;
using Xunit;

namespace NdiTelop.Tests.Services;

public class ExternalDataSourceServiceTests
{
    [Fact]
    public async Task LoadAsync_ShouldParseJsonFields()
    {
        var path = Path.GetTempFileName();
        await File.WriteAllTextAsync(path, """
        {"score_a":12,"team":{"name":"Falcons"}}
        """);

        try
        {
            var service = new ExternalDataSourceService();
            var values = await service.LoadAsync(path, CancellationToken.None);

            Assert.Equal("12", values["score_a"]);
            Assert.Equal("Falcons", values["team.name"]);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task LoadAsync_ShouldParseCsvFirstRow()
    {
        var path = Path.Combine(Path.GetTempPath(), $"data_{Guid.NewGuid():N}.csv");
        await File.WriteAllTextAsync(path, "score_a,score_b\n5,8\n");

        try
        {
            var service = new ExternalDataSourceService();
            var values = await service.LoadAsync(path, CancellationToken.None);

            Assert.Equal("5", values["score_a"]);
            Assert.Equal("8", values["score_b"]);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ApplyTemplate_ShouldReplaceKnownPlaceholders()
    {
        var service = new ExternalDataSourceService();
        var output = service.ApplyTemplate("Home {{score_a}} - {{score_b}} Away", new Dictionary<string, string>
        {
            ["score_a"] = "3",
            ["score_b"] = "1"
        });

        Assert.Equal("Home 3 - 1 Away", output);
    }

    [Fact]
    public void RenderService_ShouldUseResolvedDataSourceValues()
    {
        var service = new ExternalDataSourceService();
        var renderService = new RenderService(externalDataSourceService: service);
        var preset = new Preset { Name = "Data" };
        preset.EnsureTextBlocksInitialized();
        var block = preset.GetKeyer(KeyerDestination.Usk1).TextBlocks.Single();
        block.TextLines.Clear();
        block.TextLines.Add(new TextLine { Text = "{{score_a}}" });
        block.DataSource.IsEnabled = true;
        block.DataSource.Fields.Add(new DataSourceFieldValue { Key = "score_a", Value = "9" });

        using var bitmap = renderService.Render(preset, 640, 360);

        Assert.NotNull(bitmap);
        Assert.Equal(640, bitmap.Width);
        Assert.Equal(360, bitmap.Height);
    }

    [Fact]
    public async Task MainWindowViewModel_ShouldPopulateFieldsFromConfiguredFile()
    {
        var jsonPath = Path.Combine(Path.GetTempPath(), $"score_{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(jsonPath, "{" + "\"score_a\":7,\"score_b\":4}" );

        try
        {
            var renderService = new RenderService();
            var presetService = Substitute.For<IPresetService>();
            presetService.Presets.Returns(new List<Preset>());
            var ndiService = Substitute.For<INdiService>();
            var settingsService = Substitute.For<ISettingsService>();
            var dataSourceService = new ExternalDataSourceService();
            var vm = new MainWindowViewModel(renderService, presetService, ndiService, settingsService, dataSourceService);
            var block = vm.SelectedTextBlock!;

            block.DataSource.Source = jsonPath;
            block.DataSource.RefreshIntervalSeconds = 60;
            block.DataSource.IsEnabled = true;

            await Task.Delay(800);

            Assert.Contains(block.DataSource.Fields, field => field.Key == "score_a" && field.Value == "7");
        }
        finally
        {
            File.Delete(jsonPath);
        }
    }
}
