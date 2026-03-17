using NdiTelop.Models;
using Serilog;
using SkiaSharp;
using System.Security.Cryptography;
using System.Text;

namespace NdiTelop.Services;

public class AssetService : IDisposable
{
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".webp"
    };

    private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".mov", ".avi", ".mkv", ".webm"
    };

    private readonly string _assetDirectory;
    private readonly string _thumbnailDirectory;
    private readonly FileSystemWatcher _watcher;

    public event EventHandler? AssetsChanged;

    public AssetService(string? assetDirectory = null)
    {
        _assetDirectory = assetDirectory ?? Path.Combine(AppContext.BaseDirectory, "data", "assets");
        Directory.CreateDirectory(_assetDirectory);

        _thumbnailDirectory = Path.Combine(_assetDirectory, ".thumbs");
        Directory.CreateDirectory(_thumbnailDirectory);

        _watcher = new FileSystemWatcher(_assetDirectory)
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.DirectoryName,
            EnableRaisingEvents = true
        };

        _watcher.Created += (_, _) => RaiseAssetsChanged();
        _watcher.Deleted += (_, _) => RaiseAssetsChanged();
        _watcher.Renamed += (_, _) => RaiseAssetsChanged();
        _watcher.Changed += (_, _) => RaiseAssetsChanged();
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
            RaiseAssetsChanged();
            return uniqueName;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Asset import failed. Source={SourcePath}", sourcePath);
            throw;
        }
    }

    public IReadOnlyList<AssetItem> GetAssets()
    {
        if (!Directory.Exists(_assetDirectory))
        {
            return [];
        }

        return Directory.EnumerateFiles(_assetDirectory, "*", SearchOption.AllDirectories)
            .Where(path => !path.Contains(Path.Combine(_assetDirectory, ".thumbs"), StringComparison.OrdinalIgnoreCase))
            .Where(path => IsSupportedAsset(path))
            .Select(path =>
            {
                var relativePath = Path.GetRelativePath(_assetDirectory, path).Replace('\\', '/');
                return new AssetItem
                {
                    RelativePath = relativePath,
                    FullPath = path,
                    Kind = IsVideo(path) ? "Video" : "Image",
                    ThumbnailPath = EnsureThumbnail(path)
                };
            })
            .OrderBy(item => item.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public string ResolvePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        return Path.IsPathRooted(path) ? path : Path.Combine(_assetDirectory, path);
    }

    private string EnsureThumbnail(string sourcePath)
    {
        var key = Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(sourcePath)));
        var destination = Path.Combine(_thumbnailDirectory, $"{key}.png");

        var sourceLastWrite = File.GetLastWriteTimeUtc(sourcePath);
        if (File.Exists(destination) && File.GetLastWriteTimeUtc(destination) >= sourceLastWrite)
        {
            return destination;
        }

        if (IsVideo(sourcePath))
        {
            GenerateVideoThumbnail(destination, Path.GetExtension(sourcePath));
            return destination;
        }

        try
        {
            using var bitmap = SKBitmap.Decode(sourcePath);
            if (bitmap == null)
            {
                GenerateVideoThumbnail(destination, "BROKEN");
                return destination;
            }

            using var resized = bitmap.Resize(new SKImageInfo(160, 90), SKSamplingOptions.Default);
            using var image = SKImage.FromBitmap(resized ?? bitmap);
            using var data = image.Encode(SKEncodedImageFormat.Png, 80);
            using var stream = File.Open(destination, FileMode.Create, FileAccess.Write, FileShare.Read);
            data.SaveTo(stream);
        }
        catch
        {
            GenerateVideoThumbnail(destination, "ERR");
        }

        return destination;
    }

    private static void GenerateVideoThumbnail(string outputPath, string label)
    {
        using var surface = SKSurface.Create(new SKImageInfo(160, 90));
        var canvas = surface.Canvas;
        canvas.Clear(new SKColor(35, 35, 35));

        using var borderPaint = new SKPaint { Color = new SKColor(80, 80, 80), IsStroke = true, StrokeWidth = 2, IsAntialias = true };
        canvas.DrawRect(new SKRect(1, 1, 159, 89), borderPaint);

        using var textPaint = new SKPaint { Color = SKColors.White, IsAntialias = true, TextSize = 16, TextAlign = SKTextAlign.Center };
        canvas.DrawText(label.TrimStart('.').ToUpperInvariant(), 80, 50, textPaint);

        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 80);
        using var stream = File.Open(outputPath, FileMode.Create, FileAccess.Write, FileShare.Read);
        data.SaveTo(stream);
    }

    private static bool IsSupportedAsset(string path) => IsImage(path) || IsVideo(path);

    private static bool IsImage(string path) => ImageExtensions.Contains(Path.GetExtension(path));

    private static bool IsVideo(string path) => VideoExtensions.Contains(Path.GetExtension(path));

    private void RaiseAssetsChanged() => AssetsChanged?.Invoke(this, EventArgs.Empty);

    public void Dispose()
    {
        _watcher.Dispose();
    }
}
