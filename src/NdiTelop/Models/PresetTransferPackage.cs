namespace NdiTelop.Models;

public class PresetTransferPackage
{
    public string SchemaVersion { get; set; } = "1.0";
    public DateTimeOffset ExportedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public List<Preset> Presets { get; set; } = [];
}

