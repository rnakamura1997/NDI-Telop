using NdiTelop.Services;
using Xunit;

namespace NdiTelop.Tests.Services;

public class SetlistServiceTests
{
    [Fact]
    public async Task LoadSetlist_UnknownId_ShouldKeepCurrentSetlistNull()
    {
        var service = new SetlistService();

        await service.LoadSetlistAsync("unknown");

        Assert.Null(service.CurrentSetlist);
        Assert.Equal(0, service.CurrentIndex);
    }
}
