namespace NdiTelop.Models;

public class TakeRequest
{
    public string PresetId { get; set; } = string.Empty;
    public bool Immediate { get; set; } = true;
}
