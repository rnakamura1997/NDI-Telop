using NdiTelop.Models;

namespace NdiTelop.Interfaces;
public interface ISetlistService
{
    IReadOnlyList<Setlist> Setlists { get; }
    Setlist? CurrentSetlist { get; }
    int CurrentIndex { get; }
    Task LoadSetlistAsync(string id);
    Preset? Next();
    Preset? Previous();
}
