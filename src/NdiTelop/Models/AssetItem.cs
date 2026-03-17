using System.IO;

namespace NdiTelop.Models;

public class AssetItem
{
    public required string RelativePath { get; init; }
    public required string FullPath { get; init; }
    public required string ThumbnailPath { get; init; }
    public required string Kind { get; init; }

    public string FileName => Path.GetFileName(RelativePath);
}
