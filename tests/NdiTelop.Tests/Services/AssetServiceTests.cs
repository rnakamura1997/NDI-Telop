using NdiTelop.Services;
using SkiaSharp;
using Xunit;

namespace NdiTelop.Tests.Services;

public class AssetServiceTests
{
    [Fact]
    public void ImportImage_CopiesFileAndReturnsRelativeFileName()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"NdiTelopAssetTests_{Guid.NewGuid():N}");
        var sourceDir = Path.Combine(tempRoot, "source");
        var assetDir = Path.Combine(tempRoot, "assets");
        Directory.CreateDirectory(sourceDir);

        var sourcePath = Path.Combine(sourceDir, "sample.png");
        File.WriteAllBytes(sourcePath, [1, 2, 3]);

        try
        {
            using var service = new AssetService(assetDir);
            var relativePath = service.ImportImage(sourcePath);

            Assert.False(Path.IsPathRooted(relativePath));
            Assert.True(File.Exists(Path.Combine(assetDir, relativePath)));
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void ResolvePath_RelativePath_IsResolvedToAssetDirectory()
    {
        var assetDir = Path.Combine(Path.GetTempPath(), $"NdiTelopAssetTests_{Guid.NewGuid():N}");
        using var service = new AssetService(assetDir);

        var resolved = service.ResolvePath("overlay.png");

        Assert.Equal(Path.Combine(assetDir, "overlay.png"), resolved);
    }

    [Fact]
    public void GetAssets_ReturnsOnlySupportedFiles_WithGeneratedThumbnail()
    {
        var assetDir = Path.Combine(Path.GetTempPath(), $"NdiTelopAssetTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(assetDir);
        var imagePath = Path.Combine(assetDir, "sample.png");

        using (var surface = SKSurface.Create(new SKImageInfo(32, 32)))
        using (var image = surface.Snapshot())
        using (var data = image.Encode(SKEncodedImageFormat.Png, 80))
        using (var stream = File.OpenWrite(imagePath))
        {
            data.SaveTo(stream);
        }

        File.WriteAllText(Path.Combine(assetDir, "note.txt"), "ignore");

        try
        {
            using var service = new AssetService(assetDir);
            var assets = service.GetAssets();

            Assert.Single(assets);
            Assert.Equal("sample.png", assets[0].RelativePath);
            Assert.True(File.Exists(assets[0].ThumbnailPath));
        }
        finally
        {
            if (Directory.Exists(assetDir))
            {
                Directory.Delete(assetDir, recursive: true);
            }
        }
    }
}
