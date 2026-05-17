using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EffectViewer.Assets;
using EffectViewer.Projects;
using EffectViewer.Tests.TestUtilities;

namespace EffectViewer.Tests.Projects;

public sealed class PakResourceFolderSourceTests
{
    [Fact]
    public async Task PakSourceEnumeratesSafeFilesAndDecryptsContent()
    {
        byte[] pak = PakBuilder.Create(
            ("particles/spark.xml", Encoding.UTF8.GetBytes("<ParticleEffect/>")),
            ("images/Sun.png", [1, 2, 3, 4]),
            ("../outside.txt", Encoding.UTF8.GetBytes("blocked")));

        PakResourceFolderSource source = new("resources.pak", pak);

        IReadOnlyList<ResourceFolderFile> files = await source.EnumerateFilesAsync();

        Assert.Equal("resources", source.Name);
        Assert.Equal(["images/Sun.png", "particles/spark.xml"], files.Select(file => file.RelativePath).ToArray());
        Assert.True(await source.DirectoryExistsAsync("particles"));
        Assert.False(await source.DirectoryExistsAsync("compiled"));

        await using Stream stream = await source.OpenReadAsync("particles/spark.xml");
        using StreamReader reader = new(stream, Encoding.UTF8);
        Assert.Equal("<ParticleEffect/>", await reader.ReadToEndAsync());
        await Assert.ThrowsAsync<FileNotFoundException>(() => source.OpenReadAsync("../outside.txt"));
    }

    [Fact]
    public async Task ImportPakCreatesProjectAssetsFromArchive()
    {
        using TempDirectory temp = new();
        EffectProjectService service = new(new TestProjectStorageProvider(temp.Path));
        EffectProject project = await service.CreateProjectAsync("Current Project");
        byte[] pak = PakBuilder.Create(
            ("particles/spark.xml", Encoding.UTF8.GetBytes("<ParticleEffect/>")),
            ("images/Sun.png", [137, 80, 78, 71, 13, 10, 26, 10]));

        await using MemoryStream stream = new(pak);
        FolderImportResult result = await service.ImportPakAsync(project, "resources.pak", stream);

        Assert.Same(project, result.Project);
        Assert.Equal("Current Project", result.Project.Manifest.Name);
        Assert.Equal(1, result.ImageCount);
        Assert.Equal(1, result.ParticleCount);
        Assert.Equal(0, result.MissingImageCount);

        ImageAsset image = Assert.Single(result.Project.Manifest.Images);
        EffectAsset particle = Assert.Single(result.Project.Manifest.Particles);
        Assert.Equal("IMAGE_SUN", image.Id);
        Assert.Equal("assets/images/image_sun.png", image.Path);
        Assert.Equal("spark", particle.Id);
        Assert.Equal("assets/particles/spark.xml", particle.Path);
        Assert.True(File.Exists(Path.Combine(result.Project.RootPath, "assets", "images", "image_sun.png")));
        Assert.True(File.Exists(Path.Combine(result.Project.RootPath, "assets", "particles", "spark.xml")));
    }
}
