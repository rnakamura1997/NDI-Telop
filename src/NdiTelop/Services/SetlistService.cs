using NdiTelop.Interfaces;
using NdiTelop.Models;

namespace NdiTelop.Services;

public class SetlistService : ISetlistService
{
    private readonly List<Setlist> _setlists = [];

    public SetlistService(IEnumerable<Setlist>? setlists = null)
    {
        if (setlists is null)
        {
            return;
        }

        _setlists.AddRange(setlists);
    }

    public IReadOnlyList<Setlist> Setlists => _setlists;
    public Setlist? CurrentSetlist { get; private set; }
    public int CurrentIndex { get; private set; }

    public Task LoadSetlistAsync(string id)
    {
        CurrentSetlist = _setlists.FirstOrDefault(x => x.Id == id);
        CurrentIndex = CurrentSetlist?.PresetIds.Count > 0 ? 0 : -1;
        return Task.CompletedTask;
    }

    public Preset? Next()
    {
        if (CurrentSetlist?.PresetIds.Count is not > 0)
        {
            CurrentIndex = -1;
            return null;
        }

        if (CurrentIndex < 0)
        {
            CurrentIndex = 0;
            return CreatePreset(CurrentSetlist.PresetIds[CurrentIndex]);
        }

        if (CurrentIndex >= CurrentSetlist.PresetIds.Count - 1)
        {
            return null;
        }

        CurrentIndex++;
        return CreatePreset(CurrentSetlist.PresetIds[CurrentIndex]);
    }

    public Preset? Previous()
    {
        if (CurrentSetlist?.PresetIds.Count is not > 0)
        {
            CurrentIndex = -1;
            return null;
        }

        if (CurrentIndex <= 0)
        {
            CurrentIndex = 0;
            return null;
        }

        CurrentIndex--;
        return CreatePreset(CurrentSetlist.PresetIds[CurrentIndex]);
    }

    private static Preset CreatePreset(string id)
        => new() { Id = id };
}
