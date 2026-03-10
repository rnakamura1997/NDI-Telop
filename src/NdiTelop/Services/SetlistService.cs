using NdiTelop.Interfaces;
using NdiTelop.Models;

namespace NdiTelop.Services;

public class SetlistService : ISetlistService
{
    private readonly List<Setlist> _setlists = [];

    public IReadOnlyList<Setlist> Setlists => _setlists;
    public Setlist? CurrentSetlist { get; private set; }
    public int CurrentIndex { get; private set; }

    public Task LoadSetlistAsync(string id)
    {
        CurrentSetlist = _setlists.FirstOrDefault(x => x.Id == id);
        CurrentIndex = 0;
        return Task.CompletedTask;
    }

    public Preset? Next() => null;

    public Preset? Previous() => null;
}
