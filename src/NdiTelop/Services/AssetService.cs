using Serilog;

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

        try
        {
            Directory.CreateDirectory(_assetDirectory);

            var extension = Path.GetExtension(sourcePath);
            var safeName = Path.GetFileNameWithoutExtension(sourcePath);
            var uniqueName = $"{safeName}_{DateTime.UtcNow:yyyyMMddHHmmssfff}{extension}";
            var destinationPath = Path.Combine(_assetDirectory, uniqueName);

            File.Copy(sourcePath, destinationPath, overwrite: false);
            Log.Information("Asset imported. Source={SourcePath}, Destination={DestinationPath}", sourcePath, destinationPath);
            return uniqueName;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Asset import failed. Source={SourcePath}", sourcePath);
            throw;
        }
    }

    public string ResolvePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        return Path.IsPathRooted(path) ? path : Path.Combine(_assetDirectory, path);
    }
}
