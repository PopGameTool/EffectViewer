using EffectViewer.Projects;
using EffectViewer.Rendering;
using EffectViewer.Rendering.Export;
using EffectViewer.Runtime;
using EffectViewer.Runtime.Lua;
using EffectViewer.Tests.TestUtilities;

namespace EffectViewer.Tests.Smoke;

public sealed class QuickStartShowcaseSmokeTests
{
    private static readonly byte[] PngHeader = [137, 80, 78, 71, 13, 10, 26, 10];

    [Fact]
    public async Task QuickStartShowcaseImportsRunsAndExportsPreviewPng()
    {
        using TempDirectory temp = new();
        EffectProjectService service = new(new TestProjectStorageProvider(temp.Path));

        await using FileStream sample = File.OpenRead(
            Path.Combine(FindRepositoryRoot(), "Samples", "QuickStartShowcase.zip"));
        EffectProject project = await service.ImportProjectZipAsync(sample);

        ShowcaseAsset showcase = Assert.Single(project.Manifest.Showcases);
        Assert.Equal("Quick Start Showcase", project.Manifest.Name);
        Assert.Equal("quickstart_showcase", showcase.Id);

        string scriptPath = Path.Combine(project.RootPath, showcase.Path.Replace('/', Path.DirectorySeparatorChar));
        string script = await File.ReadAllTextAsync(scriptPath);

        using EffectWorld world = new();
        world.LoadProject(project);
        LuaRunResult result = new LuaHost(world).Run(script);

        Assert.True(result.Success, string.Join(Environment.NewLine, result.Logs));
        Assert.NotNull(result.FrameProvider);
        Assert.Contains("Quick Start Showcase loaded", result.Logs);

        RenderFrame frame = result.FrameProvider.GetFrame(0.25);
        Assert.NotEmpty(frame.Meshes);

        using MemoryStream png = new();
        PreviewExportWriter.WritePng(
            png,
            frame,
            textureSource: null!,
            new PreviewExportOptions
            {
                ReferenceWidth = 800,
                ReferenceHeight = 600,
                CanvasScale = 1
            });

        byte[] bytes = png.ToArray();
        Assert.True(bytes.Length > PngHeader.Length);
        Assert.Equal(PngHeader, bytes.Take(PngHeader.Length).ToArray());
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "EffectViewer.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not find the EffectViewer repository root.");
    }
}
