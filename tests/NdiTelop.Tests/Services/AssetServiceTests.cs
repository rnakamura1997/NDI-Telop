using NdiTelop.Services;
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
            var service = new AssetService(assetDir);
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
        var service = new AssetService(assetDir);

        var resolved = service.ResolvePath("overlay.png");

        Assert.Equal(Path.Combine(assetDir, "overlay.png"), resolved);
    }
}
