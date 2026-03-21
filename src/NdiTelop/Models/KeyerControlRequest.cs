namespace NdiTelop.Models;

public class KeyerControlRequest
{
    public bool? KeyOn { get; set; }
    public double? Opacity { get; set; }
    public string Action { get; set; } = string.Empty;
}
