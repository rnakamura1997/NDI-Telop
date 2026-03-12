using System;
using System.IO;

namespace NdiTelop.Services;

public class AssetService
{
    private readonly string _assetDirectory;

    public AssetService(string? assetDirectory = null)
    {
        _assetDirectory = assetDirectory ?? Path.Combine(AppContext.BaseDirectory, "data", "assets");
    }

    public string ImportImage(string sourcePath)
    {
        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            throw new ArgumentException("Source path is required.", nameof(sourcePath));
        }

        Directory.CreateDirectory(_assetDirectory);

        var extension = Path.GetExtension(sourcePath);
        var safeName = Path.GetFileNameWithoutExtension(sourcePath);
        var uniqueName = $"{safeName}_{DateTime.UtcNow:yyyyMMddHHmmssfff}{extension}";
        var destinationPath = Path.Combine(_assetDirectory, uniqueName);

        File.Copy(sourcePath, destinationPath, overwrite: false);
        return uniqueName;
    }

    public string ResolvePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        if (Path.IsPathRooted(path))
        {
            return path;
        }

        return Path.Combine(_assetDirectory, path);
    }
}
