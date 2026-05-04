using System.IO.Compression;
using System.Text;
using System.Text.Json;
using EffectViewer.Projects;
using EffectViewer.Tests.TestUtilities;

namespace EffectViewer.Tests.Projects;

public sealed class EffectProjectServiceTests
{
    [Fact]
    public async Task ProjectZipRoundTripsManifestAndResourceFiles()
    {
        using TempDirectory temp = new();
        EffectProjectService service = CreateService(temp);
        EffectProject project = await service.CreateProjectAsync("Risky Demo");
        ProjectResourceResult script = await service.CreateResourceAsync(project, EffectAssetKind.Showcase, "Intro Scene");

        Assert.Equal(EffectAssetKind.Showcase, script.Kind);
        Assert.Equal("intro_scene", script.AssetId);
        Assert.Equal("scripts/intro_scene.lua", script.ProjectPath);

        await using MemoryStream exportedScript = new();
        await service.ExportProjectFileAsync(project, script.ProjectPath, exportedScript);
        Assert.Contains("intro_scene initialized", Encoding.UTF8.GetString(exportedScript.ToArray()));

        await using MemoryStream zip = new();
        await service.ExportProjectZipAsync(project, zip);
        zip.Position = 0;

        EffectProject imported = await service.ImportProjectZipAsync(zip);

        Assert.NotEqual(project.RootPath, imported.RootPath);
        ShowcaseAsset importedShowcase = Assert.Single(imported.Manifest.Showcases);
        Assert.Equal("intro_scene", importedShowcase.Id);
        Assert.Equal("scripts/intro_scene.lua", importedShowcase.Path);
        Assert.True(File.Exists(Path.Combine(imported.RootPath, "scripts", "intro_scene.lua")));
        Assert.Equal(
            ["Risky Demo", "Risky Demo"],
            (await service.ListProjectsAsync()).Select(item => item.Name).ToArray());
    }

    [Fact]
    public async Task ImportProjectZipRejectsEntriesOutsideProjectRoot()
    {
        using TempDirectory temp = new();
        EffectProjectService service = CreateService(temp);
        await using MemoryStream zip = new();

        using (ZipArchive archive = new(zip, ZipArchiveMode.Create, leaveOpen: true))
        {
            ZipArchiveEntry manifest = archive.CreateEntry(EffectProjectService.ManifestFileName);
            await using (Stream stream = manifest.Open())
            {
                await JsonSerializer.SerializeAsync(stream, new ProjectManifest { Name = "Unsafe Import" });
            }

            ZipArchiveEntry escape = archive.CreateEntry("../escape.txt");
            await using (Stream stream = escape.Open())
            await using (StreamWriter writer = new(stream, Encoding.UTF8))
            {
                await writer.WriteAsync("outside");
            }
        }

        zip.Position = 0;

        await Assert.ThrowsAsync<InvalidDataException>(() => service.ImportProjectZipAsync(zip));
        Assert.False(File.Exists(temp.GetPath("escape.txt")));
        Assert.Empty(Directory.EnumerateDirectories(temp.Path));
    }

    private static EffectProjectService CreateService(TempDirectory temp)
    {
        return new EffectProjectService(new TestProjectStorageProvider(temp.Path));
    }
}
