using System.Text;
using EffectViewer.Assets;
using EffectViewer.EffectRuntime.Particle;
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

    [Fact]
    public async Task ImportFolderIntoCurrentProjectKeepsProjectAndAddsAssets()
    {
        using TempDirectory temp = new();
        string sourceDirectory = temp.GetPath("source-assets");
        Directory.CreateDirectory(Path.Combine(sourceDirectory, "images"));
        await File.WriteAllBytesAsync(Path.Combine(sourceDirectory, "images", "Sun Beam.png"), PngHeader);

        EffectProjectService service = new(new TestProjectStorageProvider(temp.GetPath("projects")));
        EffectProject project = await service.CreateProjectAsync("Current Project");

        FolderImportResult result = await service.ImportFolderAsync(project, sourceDirectory);

        Assert.Same(project, result.Project);
        Assert.Equal("Current Project", project.Manifest.Name);
        ImageAsset image = Assert.Single(project.Manifest.Images);
        Assert.Equal("IMAGE_SUN_BEAM", image.Id);
        Assert.True(File.Exists(Path.Combine(project.RootPath, "assets", "images", "image_sun_beam.png")));
        Assert.Single(await service.ListProjectsAsync());
    }

    [Fact]
    public async Task ImportFolderKeepBothRemapsImportedEffectImageReferences()
    {
        using TempDirectory temp = new();
        string sourceDirectory = temp.GetPath("source-assets");
        Directory.CreateDirectory(Path.Combine(sourceDirectory, "images"));
        Directory.CreateDirectory(Path.Combine(sourceDirectory, "particles"));
        await File.WriteAllBytesAsync(Path.Combine(sourceDirectory, "images", "Sun.png"), [.. PngHeader, 2]);
        await File.WriteAllBytesAsync(Path.Combine(sourceDirectory, "particles", "spark.xml"), CreateParticleXml("IMAGE_SUN"));

        EffectProjectService service = new(new TestProjectStorageProvider(temp.GetPath("projects")));
        EffectProject project = await service.CreateProjectAsync("Current Project");
        await using (MemoryStream existingImage = new([.. PngHeader, 1]))
        {
            await service.ImportResourceFileAsync(project, "Sun.png", existingImage);
        }

        FolderImportResult result = await service.ImportFolderAsync(
            project,
            sourceDirectory,
            conflictResolver: _ => Task.FromResult(ImportConflictResolution.KeepBoth));

        Assert.Equal(1, result.ImageCount);
        Assert.Equal(1, result.ParticleCount);
        Assert.Equal(["IMAGE_SUN", "IMAGE_SUN_2"], project.Manifest.Images.Select(asset => asset.Id).ToArray());
        EffectAsset particle = Assert.Single(project.Manifest.Particles);
        await using FileStream particleStream = File.OpenRead(Path.Combine(project.RootPath, particle.Path));
        ParticleDefinition definition = ParticleDefinitionCodec.Decode(particleStream);
        Assert.Equal("IMAGE_SUN_2", definition.mEmitterDefs[0].mImage);
    }

    [Fact]
    public async Task ImportFolderOverwriteReplacesExistingAssetBytes()
    {
        using TempDirectory temp = new();
        string sourceDirectory = temp.GetPath("source-assets");
        Directory.CreateDirectory(Path.Combine(sourceDirectory, "images"));
        byte[] newBytes = [.. PngHeader, 2];
        await File.WriteAllBytesAsync(Path.Combine(sourceDirectory, "images", "Sun.png"), newBytes);

        EffectProjectService service = new(new TestProjectStorageProvider(temp.GetPath("projects")));
        EffectProject project = await service.CreateProjectAsync("Current Project");
        await using (MemoryStream existingImage = new([.. PngHeader, 1]))
        {
            await service.ImportResourceFileAsync(project, "Sun.png", existingImage);
        }

        FolderImportResult result = await service.ImportFolderAsync(
            project,
            sourceDirectory,
            conflictResolver: _ => Task.FromResult(ImportConflictResolution.Overwrite));

        Assert.Equal(1, result.ImageCount);
        ImageAsset image = Assert.Single(project.Manifest.Images);
        Assert.Equal("IMAGE_SUN", image.Id);
        Assert.Equal(newBytes, await File.ReadAllBytesAsync(Path.Combine(project.RootPath, image.Path)));
    }

    [Fact]
    public async Task ImportFolderSkipLeavesExistingAssetUnchanged()
    {
        using TempDirectory temp = new();
        string sourceDirectory = temp.GetPath("source-assets");
        Directory.CreateDirectory(Path.Combine(sourceDirectory, "images"));
        await File.WriteAllBytesAsync(Path.Combine(sourceDirectory, "images", "Sun.png"), [.. PngHeader, 2]);

        EffectProjectService service = new(new TestProjectStorageProvider(temp.GetPath("projects")));
        EffectProject project = await service.CreateProjectAsync("Current Project");
        byte[] existingBytes = [.. PngHeader, 1];
        await using (MemoryStream existingImage = new(existingBytes))
        {
            await service.ImportResourceFileAsync(project, "Sun.png", existingImage);
        }

        FolderImportResult result = await service.ImportFolderAsync(
            project,
            sourceDirectory,
            conflictResolver: _ => Task.FromResult(ImportConflictResolution.Skip));

        Assert.Equal(0, result.ImageCount);
        ImageAsset image = Assert.Single(project.Manifest.Images);
        Assert.Equal(existingBytes, await File.ReadAllBytesAsync(Path.Combine(project.RootPath, image.Path)));
    }

    private static byte[] CreateParticleXml(string imageId)
    {
        ParticleDefinition definition = new()
        {
            mEmitterDefs = [new ParticleEmitterDefinition { mImage = imageId }],
            mEmitterDefCount = 1
        };

        using MemoryStream stream = new();
        ParticleDefinitionCodec.WriteXml(stream, definition);
        return stream.ToArray();
    }
}
