namespace NdiTelop.Models;

public class Setlist
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public List<string> PresetIds { get; set; } = [];
}
