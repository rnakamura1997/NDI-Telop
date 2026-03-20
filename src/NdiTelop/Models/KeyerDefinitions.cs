namespace NdiTelop.Models;

public static class KeyerDefinitions
{
    public static readonly KeyerDestination[] OrderedDestinations =
    [
        KeyerDestination.Usk1,
        KeyerDestination.Usk2,
        KeyerDestination.Usk3,
        KeyerDestination.Usk4,
        KeyerDestination.Dsk1,
        KeyerDestination.Dsk2,
        KeyerDestination.Dsk3,
        KeyerDestination.Dsk4
    ];

    public static KeyerBusType ToBusType(this KeyerDestination destination)
        => destination is KeyerDestination.Usk1 or KeyerDestination.Usk2 or KeyerDestination.Usk3 or KeyerDestination.Usk4
            ? KeyerBusType.Usk
            : KeyerBusType.Dsk;

    public static int ToDefaultPriority(this KeyerDestination destination)
        => destination switch
        {
            KeyerDestination.Usk1 or KeyerDestination.Dsk1 => 1,
            KeyerDestination.Usk2 or KeyerDestination.Dsk2 => 2,
            KeyerDestination.Usk3 or KeyerDestination.Dsk3 => 3,
            _ => 4
        };

    public static string ToDisplayName(this KeyerDestination destination)
        => destination switch
        {
            KeyerDestination.Usk1 => "USK1",
            KeyerDestination.Usk2 => "USK2",
            KeyerDestination.Usk3 => "USK3",
            KeyerDestination.Usk4 => "USK4",
            KeyerDestination.Dsk1 => "DSK1",
            KeyerDestination.Dsk2 => "DSK2",
            KeyerDestination.Dsk3 => "DSK3",
            KeyerDestination.Dsk4 => "DSK4",
            _ => destination.ToString()
        };
}
