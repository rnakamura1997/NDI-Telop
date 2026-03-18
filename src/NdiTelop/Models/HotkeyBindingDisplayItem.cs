namespace NdiTelop.Models;

public sealed class HotkeyBindingDisplayItem
{
    public required string ActionName { get; init; }
    public required string Shortcut { get; init; }
    public required string RegistrationStatus { get; init; }
    public string Summary => $"{Shortcut} — {ActionName}";
}
