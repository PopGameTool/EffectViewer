using System.IO.Compression;
using System.Text;
using System.Text.Json;
using EffectViewer.Assets;
using EffectViewer.Projects;
using EffectViewer.Tests.TestUtilities;

namespace EffectViewer.Tests.Projects;

public sealed class EffectProjectServiceTests
{
    private static readonly byte[] PngHeader = [137, 80, 78, 71, 13, 10, 26, 10];

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

    [Fact]
    public async Task RenameProjectUsesUniqueDirectoryAndPersistsManifestName()
    {
        using TempDirectory temp = new();
        EffectProjectService service = CreateService(temp);
        EffectProject first = await service.CreateProjectAsync("Release Demo");
        EffectProject second = await service.CreateProjectAsync("Scratch");

        EffectProject renamed = await service.RenameProjectAsync(second.RootPath, "Release Demo");

        Assert.Equal("Release Demo", renamed.Manifest.Name);
        Assert.Equal("release_demo", Path.GetFileName(first.RootPath));
        Assert.Equal("release_demo-2", Path.GetFileName(renamed.RootPath));
        Assert.False(Directory.Exists(second.RootPath));
        Assert.True(File.Exists(Path.Combine(renamed.RootPath, EffectProjectService.ManifestFileName)));

        IReadOnlyList<ProjectInfo> projects = await service.ListProjectsAsync();
        Assert.Equal(["release_demo", "release_demo-2"], projects.Select(project => project.DirectoryName).Order().ToArray());
    }

    [Fact]
    public async Task ImportResourceFileCreatesStableUniqueImageAssetsAndExportsBytes()
    {
        using TempDirectory temp = new();
        EffectProjectService service = CreateService(temp);
        EffectProject project = await service.CreateProjectAsync("Import Demo");

        ProjectResourceResult first = await ImportBytesAsync(service, project, "Sun Beam.PNG", PngHeader);
        ProjectResourceResult second = await ImportBytesAsync(service, project, "Sun Beam.PNG", [.. PngHeader, 1, 2, 3]);

        Assert.Equal("IMAGE_SUN_BEAM", first.AssetId);
        Assert.Equal("assets/images/image_sun_beam.png", first.ProjectPath);
        Assert.Equal("IMAGE_SUN_BEAM_2", second.AssetId);
        Assert.Equal("assets/images/image_sun_beam_2.png", second.ProjectPath);

        ImageAsset[] images = project.Manifest.Images.ToArray();
        Assert.Equal(["IMAGE_SUN_BEAM", "IMAGE_SUN_BEAM_2"], images.Select(image => image.Id).ToArray());
        Assert.All(images, image =>
        {
            Assert.Equal(1, image.Rows);
            Assert.Equal(1, image.Cols);
        });

        await using MemoryStream exported = new();
        await service.ExportProjectFileAsync(project, second.ProjectPath, exported);
        Assert.Equal([.. PngHeader, 1, 2, 3], exported.ToArray());
    }

    [Fact]
    public async Task ImportResourceFileRejectsUnsupportedTypeWithoutMutatingProject()
    {
        using TempDirectory temp = new();
        EffectProjectService service = CreateService(temp);
        EffectProject project = await service.CreateProjectAsync("Unsupported Import Demo");

        await using MemoryStream stream = new(Encoding.UTF8.GetBytes("not an effect resource"));

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            service.ImportResourceFileAsync(project, "notes.txt", stream));
        Assert.Empty(project.Manifest.Images);
        Assert.Empty(project.Manifest.Fonts);
        Assert.Empty(project.Manifest.Reanims);
        Assert.Empty(project.Manifest.Particles);
        Assert.Empty(project.Manifest.Trails);
        Assert.Empty(project.Manifest.Showcases);
        Assert.Equal([EffectProjectService.ManifestFileName], Directory
            .EnumerateFiles(project.RootPath)
            .Select(path => Path.GetFileName(path)!)
            .ToArray());
    }

    [Fact]
    public async Task DeleteResourceRemovesOnlySelectedManifestEntryAndFile()
    {
        using TempDirectory temp = new();
        EffectProjectService service = CreateService(temp);
        EffectProject project = await service.CreateProjectAsync("Delete Demo");
        ProjectResourceResult first = await service.CreateResourceAsync(project, EffectAssetKind.Showcase, "Intro Scene");
        ProjectResourceResult second = await service.CreateResourceAsync(project, EffectAssetKind.Showcase, "Intro Scene");

        await service.DeleteResourceAsync(project, EffectAssetKind.Showcase, first.AssetId, first.ProjectPath);

        Assert.Equal([second.AssetId], project.Manifest.Showcases.Select(showcase => showcase.Id).ToArray());
        Assert.False(File.Exists(Path.Combine(project.RootPath, "scripts", "intro_scene.lua")));
        Assert.True(File.Exists(Path.Combine(project.RootPath, "scripts", "intro_scene_2.lua")));

        EffectProject reloaded = await service.LoadAsync(project.RootPath);
        ShowcaseAsset remaining = Assert.Single(reloaded.Manifest.Showcases);
        Assert.Equal(second.AssetId, remaining.Id);
        Assert.Equal(second.ProjectPath, remaining.Path);
    }

    [Fact]
    public async Task ExportProjectFileRejectsPathsOutsideProjectRoot()
    {
        using TempDirectory temp = new();
        EffectProjectService service = CreateService(temp);
        EffectProject project = await service.CreateProjectAsync("Safe Export");

        await using MemoryStream output = new();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ExportProjectFileAsync(project, "../escape.txt", output));
    }

    [Fact]
    public async Task LoadNormalizesLegacyManifestDefaultsAndReanimTweens()
    {
        using TempDirectory temp = new();
        EffectProjectService service = CreateService(temp);
        string projectDirectory = temp.GetPath("legacy");
        Directory.CreateDirectory(projectDirectory);
        await File.WriteAllTextAsync(
            Path.Combine(projectDirectory, EffectProjectService.ManifestFileName),
            """
            {
              "version": 0,
              "name": "Legacy Demo",
              "images": [
                { "id": "IMAGE_BAD", "path": "assets/images/bad.png", "rows": 0, "cols": -3 }
              ],
              "fonts": [
                { "id": "MAIN_FONT", "path": "assets/fonts/main.ttf", "trueType": true, "fontSize": 0, "borderSize": -4 }
              ],
              "reanims": [
                {
                  "id": "intro",
                  "path": "assets/reanims/intro.reanim",
                  "tweens": [
                    { "trackIndex": -5, "trackName": null, "startFrame": 1, "endFrame": 3, "anchorX": 0.25, "anchorY": 0.75, "properties": [ "x" ] },
                    { "trackIndex": 1, "startFrame": 5, "endFrame": 2 }
                  ]
                }
              ],
              "particles": null,
              "trails": null,
              "showcases": null
            }
            """);

        EffectProject project = await service.LoadAsync(projectDirectory);

        Assert.Equal(1, project.Manifest.Version);
        ImageAsset image = Assert.Single(project.Manifest.Images);
        Assert.Equal(1, image.Rows);
        Assert.Equal(1, image.Cols);

        FontAsset font = Assert.Single(project.Manifest.Fonts);
        Assert.True(font.TrueType);
        Assert.Equal(32, font.FontSize);
        Assert.Equal(0, font.BorderSize);

        ReanimAsset reanim = Assert.Single(project.Manifest.Reanims);
        ReanimTween tween = Assert.Single(reanim.Tweens);
        Assert.Equal(0, tween.TrackIndex);
        Assert.Equal(string.Empty, tween.TrackName);
        Assert.Equal(1, tween.StartFrame);
        Assert.Equal(3, tween.EndFrame);
        Assert.Equal(ReanimTween.CreateTweenedProperties(), tween.Properties);
        Assert.Empty(project.Manifest.Particles);
        Assert.Empty(project.Manifest.Trails);
        Assert.Empty(project.Manifest.Showcases);
    }

    private static EffectProjectService CreateService(TempDirectory temp)
    {
        return new EffectProjectService(new TestProjectStorageProvider(temp.Path));
    }

    private static async Task<ProjectResourceResult> ImportBytesAsync(
        EffectProjectService service,
        EffectProject project,
        string fileName,
        byte[] bytes)
    {
        await using MemoryStream stream = new(bytes);
        return await service.ImportResourceFileAsync(project, fileName, stream);
    }
}
