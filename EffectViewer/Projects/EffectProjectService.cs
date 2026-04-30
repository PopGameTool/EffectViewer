using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using EffectViewer.Assets;

namespace EffectViewer.Projects
{
    public sealed class EffectProjectService
    {
        public const string ManifestFileName = "project.effectproj.json";

        private static readonly JsonSerializerOptions SerializerOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };

        public async Task<EffectProject> LoadAsync(string projectDirectory)
        {
            string manifestPath = System.IO.Path.Combine(projectDirectory, ManifestFileName);
            await using FileStream stream = File.OpenRead(manifestPath);
            ProjectManifest manifest = await JsonSerializer.DeserializeAsync<ProjectManifest>(stream, SerializerOptions)
                ?? new ProjectManifest();

            Normalize(manifest);
            return new EffectProject(projectDirectory, manifest);
        }

        public async Task SaveAsync(EffectProject project)
        {
            Directory.CreateDirectory(project.RootPath);
            string manifestPath = System.IO.Path.Combine(project.RootPath, ManifestFileName);
            await using FileStream stream = File.Create(manifestPath);
            await JsonSerializer.SerializeAsync(stream, project.Manifest, SerializerOptions);
        }

        public EffectProject CreateNew(string projectDirectory, string projectName)
        {
            ProjectManifest manifest = new()
            {
                Name = string.IsNullOrWhiteSpace(projectName) ? "Untitled Effect Project" : projectName
            };

            return new EffectProject(projectDirectory, manifest);
        }

        public EffectProject CreateDemoProject()
        {
            ProjectManifest manifest = new()
            {
                Name = "Demo Effect Project",
                Images =
                [
                    new ImageAsset { Id = "fire_sheet", Path = "images/fire.png", Rows = 4, Cols = 8 },
                    new ImageAsset { Id = "slash_trail", Path = "images/slash.png", Rows = 1, Cols = 1 }
                ],
                Reanims =
                [
                    new EffectAsset { Id = "sample_reanim", Path = "reanim/sample.reanim" }
                ],
                Particles =
                [
                    new EffectAsset { Id = "fire_burst", Path = "particles/fire_burst.particle" }
                ],
                Trails =
                [
                    new EffectAsset { Id = "sword_slash", Path = "trails/sword_slash.trail" }
                ],
                Showcases =
                [
                    new ShowcaseAsset { Id = "demo_scene", Path = "scripts/demo.lua" }
                ]
            };

            return new EffectProject(string.Empty, manifest);
        }

        public async Task<FolderImportResult> ImportFolderAsync(string sourceDirectory, string projectDirectory, ImportMode mode)
        {
            PopCapResourceFolderImporter importer = new();
            FolderImportResult result = importer.Import(sourceDirectory, projectDirectory, mode);

            if (!string.IsNullOrWhiteSpace(projectDirectory))
            {
                await SaveAsync(result.Project);
            }

            return result;
        }

        private static void Normalize(ProjectManifest manifest)
        {
            foreach (ImageAsset image in manifest.Images)
            {
                image.Rows = image.Rows < 1 ? 1 : image.Rows;
                image.Cols = image.Cols < 1 ? 1 : image.Cols;
            }
        }
    }
}
