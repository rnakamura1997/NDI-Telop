using NdiTelop.Models;
using NdiTelop.Services;
using Xunit;

namespace NdiTelop.Tests.Services;

public class SetlistServiceTests
{
    [Fact]
    public async Task LoadSetlistAsync_ExistingSetlist_SetsCurrentSetlistAndIndexZero()
    {
        var setlist = new Setlist { Id = "setlist-1", PresetIds = ["preset-1", "preset-2"] };
        var sut = new SetlistService([setlist]);

        await sut.LoadSetlistAsync("setlist-1");

        Assert.Same(setlist, sut.CurrentSetlist);
        Assert.Equal(0, sut.CurrentIndex);
    }

    [Fact]
    public async Task LoadSetlistAsync_NotFound_ClearsCurrentSetlistAndSetsIndexMinusOne()
    {
        var sut = new SetlistService([new Setlist { Id = "setlist-1", PresetIds = ["preset-1"] }]);

        await sut.LoadSetlistAsync("not-found");

        Assert.Null(sut.CurrentSetlist);
        Assert.Equal(-1, sut.CurrentIndex);
    }

    [Fact]
    public async Task Next_WhenMiddleItem_MovesForwardAndReturnsPreset()
    {
        var setlist = new Setlist { Id = "setlist-1", PresetIds = ["preset-1", "preset-2", "preset-3"] };
        var sut = new SetlistService([setlist]);
        await sut.LoadSetlistAsync(setlist.Id);

        var next = sut.Next();

        Assert.NotNull(next);
        Assert.Equal("preset-2", next!.Id);
        Assert.Equal(1, sut.CurrentIndex);
    }

    [Fact]
    public async Task Next_WhenAlreadyAtLast_ReturnsNullAndKeepsIndex()
    {
        var setlist = new Setlist { Id = "setlist-1", PresetIds = ["preset-1", "preset-2"] };
        var sut = new SetlistService([setlist]);
        await sut.LoadSetlistAsync(setlist.Id);
        _ = sut.Next();

        var next = sut.Next();

        Assert.Null(next);
        Assert.Equal(1, sut.CurrentIndex);
    }

    [Fact]
    public async Task Previous_WhenMiddleItem_MovesBackwardAndReturnsPreset()
    {
        var setlist = new Setlist { Id = "setlist-1", PresetIds = ["preset-1", "preset-2"] };
        var sut = new SetlistService([setlist]);
        await sut.LoadSetlistAsync(setlist.Id);
        _ = sut.Next();

        var previous = sut.Previous();

        Assert.NotNull(previous);
        Assert.Equal("preset-1", previous!.Id);
        Assert.Equal(0, sut.CurrentIndex);
    }

    [Fact]
    public async Task Previous_WhenAlreadyAtFirst_ReturnsNullAndKeepsIndexAtZero()
    {
        var setlist = new Setlist { Id = "setlist-1", PresetIds = ["preset-1", "preset-2"] };
        var sut = new SetlistService([setlist]);
        await sut.LoadSetlistAsync(setlist.Id);

        var previous = sut.Previous();

        Assert.Null(previous);
        Assert.Equal(0, sut.CurrentIndex);
    }

    [Fact]
    public async Task NextPrevious_WithEmptySetlist_ReturnNullAndIndexMinusOne()
    {
        var setlist = new Setlist { Id = "setlist-1", PresetIds = [] };
        var sut = new SetlistService([setlist]);
        await sut.LoadSetlistAsync(setlist.Id);

        var next = sut.Next();
        var previous = sut.Previous();

        Assert.Null(next);
        Assert.Null(previous);
        Assert.Equal(-1, sut.CurrentIndex);
    }
}
