using NdiTelop.Services;
using Xunit;

namespace NdiTelop.Tests.Services;

public class PresetServiceTests
{
    [Fact]
    public async Task LoadPresetsAsync_ShouldNotThrow()
    {
        var service = new PresetService();
        await service.LoadPresetsAsync();
        Assert.NotNull(service.Presets);
    }
}
