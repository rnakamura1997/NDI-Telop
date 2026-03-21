namespace NdiTelop.Models;

public static class KeyerDestinationParser
{
    public static bool TryParse(string? value, out KeyerDestination destination)
    {
        destination = KeyerDestination.Usk1;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = value.Trim().Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace("_", string.Empty, StringComparison.Ordinal)
            .ToUpperInvariant();

        destination = normalized switch
        {
            "USK1" => KeyerDestination.Usk1,
            "USK2" => KeyerDestination.Usk2,
            "USK3" => KeyerDestination.Usk3,
            "USK4" => KeyerDestination.Usk4,
            "DSK1" => KeyerDestination.Dsk1,
            "DSK2" => KeyerDestination.Dsk2,
            "DSK3" => KeyerDestination.Dsk3,
            "DSK4" => KeyerDestination.Dsk4,
            _ => destination
        };

        return normalized is "USK1" or "USK2" or "USK3" or "USK4" or "DSK1" or "DSK2" or "DSK3" or "DSK4";
    }
}
