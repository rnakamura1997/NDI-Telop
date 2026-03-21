using NdiTelop.Models;

namespace NdiTelop.Services;

public class PlaylistController
{
    public PlaylistItem? GetCurrent(IReadOnlyList<PlaylistItem> items, int currentIndex)
        => currentIndex >= 0 && currentIndex < items.Count ? items[currentIndex] : null;

    public PlaylistItem? GetNext(IReadOnlyList<PlaylistItem> items, int currentIndex)
    {
        var nextIndex = currentIndex + 1;
        return nextIndex >= 0 && nextIndex < items.Count ? items[nextIndex] : null;
    }

    public PlaylistItem? Advance(IReadOnlyList<PlaylistItem> items, int currentIndex)
    {
        if (items.Count == 0)
        {
            return null;
        }

        var targetIndex = currentIndex < 0 ? 0 : currentIndex + 1;
        return targetIndex >= 0 && targetIndex < items.Count ? items[targetIndex] : null;
    }
}
