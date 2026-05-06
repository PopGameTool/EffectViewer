using System.Text;
using EffectViewer.Assets;
using EffectViewer.Projects;
using EffectViewer.Tests.TestUtilities;

namespace EffectViewer.Tests.Projects;

public sealed class ResourceFolderImporterTests
{
    private static readonly byte[] PngHeader = [137, 80, 78, 71, 13, 10, 26, 10];

    [Fact]
    public async Task ImportFolderByConventionImportsSupportedAssetsAndIgnoresOtherFiles()
    {
        using TempDirectory temp = new();
        string sourceDirectory = temp.GetPath("source-assets");
        Directory.CreateDirectory(Path.Combine(sourceDirectory, "images"));
        Directory.CreateDirectory(Path.Combine(sourceDirectory, "particles"));
        await File.WriteAllBytesAsync(Path.Combine(sourceDirectory, "images", "Sun Beam.png"), PngHeader);
        await File.WriteAllTextAsync(Path.Combine(sourceDirectory, "particles", "spark.xml"), "<ParticleEffect/>", Encoding.UTF8);
        await File.WriteAllTextAsync(Path.Combine(sourceDirectory, "notes.txt"), "ignored", Encoding.UTF8);

        EffectProjectService service = new(new TestProjectStorageProvider(temp.GetPath("projects")));

        FolderImportResult result = await service.ImportFolderAsync(sourceDirectory);

        Assert.Equal("source-assets", result.Project.Manifest.Name);
        Assert.Equal(1, result.ImageCount);
        Assert.Equal(1, result.ParticleCount);
        Assert.Equal(0, result.ReanimCount);
        Assert.Equal(0, result.TrailCount);
        Assert.Equal(0, result.MissingImageCount);

        ImageAsset image = Assert.Single(result.Project.Manifest.Images);
        EffectAsset particle = Assert.Single(result.Project.Manifest.Particles);
        Assert.Equal("IMAGE_SUN_BEAM", image.Id);
        Assert.Equal("assets/images/image_sun_beam.png", image.Path);
        Assert.Equal("spark", particle.Id);
        Assert.Equal("assets/particles/spark.xml", particle.Path);
        Assert.True(File.Exists(Path.Combine(result.Project.RootPath, "assets", "images", "image_sun_beam.png")));
        Assert.True(File.Exists(Path.Combine(result.Project.RootPath, "assets", "particles", "spark.xml")));
        Assert.False(File.Exists(Path.Combine(result.Project.RootPath, "notes.txt")));
    }
}
