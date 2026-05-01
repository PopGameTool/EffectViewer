using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using EffectViewer.Assets;

namespace EffectViewer.Projects
{
    public sealed class EffectProjectService
    {
        public const string ManifestFileName = "project.effectproj.json";
        public const string AssetsDirectoryName = "assets";

        private static readonly JsonSerializerOptions SerializerOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };

        private readonly IProjectStorageProvider _storageProvider;

        public EffectProjectService(IProjectStorageProvider storageProvider)
        {
            _storageProvider = storageProvider ?? new DefaultProjectStorageProvider();
        }

        public async Task<EffectProject> LoadAsync(string projectDirectory)
        {
            string manifestPath = System.IO.Path.Combine(projectDirectory, ManifestFileName);
            await using FileStream stream = File.OpenRead(manifestPath);
            ProjectManifest manifest = await JsonSerializer.DeserializeAsync<ProjectManifest>(stream, SerializerOptions)
                ?? new ProjectManifest();

            Normalize(manifest);
            return new EffectProject(projectDirectory, manifest);
        }

        public async Task<IReadOnlyList<ProjectInfo>> ListProjectsAsync()
        {
            string projectsRoot = _storageProvider.ProjectsRootPath;
            if (string.IsNullOrWhiteSpace(projectsRoot) || !Directory.Exists(projectsRoot))
            {
                return [];
            }

            List<ProjectInfo> projects = [];
            foreach (string projectDirectory in Directory.EnumerateDirectories(projectsRoot))
            {
                string manifestPath = Path.Combine(projectDirectory, ManifestFileName);
                if (!File.Exists(manifestPath))
                {
                    continue;
                }

                try
                {
                    await using FileStream stream = File.OpenRead(manifestPath);
                    ProjectManifest manifest = await JsonSerializer.DeserializeAsync<ProjectManifest>(stream, SerializerOptions)
                        ?? new ProjectManifest();

                    projects.Add(new ProjectInfo
                    {
                        Name = string.IsNullOrWhiteSpace(manifest.Name)
                            ? Path.GetFileName(projectDirectory)
                            : manifest.Name,
                        ProjectPath = projectDirectory,
                        DirectoryName = Path.GetFileName(projectDirectory),
                        LastModified = File.GetLastWriteTime(manifestPath)
                    });
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
                {
                }
            }

            return projects
                .OrderByDescending(project => project.LastModified)
                .ThenBy(project => project.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
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

        public async Task<FolderImportResult> ImportFolderAsync(string sourceDirectory)
        {
            string sourceName = Path.GetFileName(sourceDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            string projectDirectory = CreateUniqueProjectDirectory(sourceName);
            PopCapResourceFolderImporter importer = new();
            FolderImportResult result = importer.Import(sourceDirectory, projectDirectory);

            await SaveAsync(result.Project);

            return result;
        }

        public async Task<EffectProject> ImportProjectZipAsync(Stream zipStream, IProgress<ProjectTransferProgress> progress = null)
        {
            if (zipStream is null)
            {
                throw new ArgumentNullException(nameof(zipStream));
            }

            progress?.Report(new ProjectTransferProgress
            {
                Operation = "Importing Project",
                Message = "Reading archive"
            });

            using ZipArchive archive = new(zipStream, ZipArchiveMode.Read, leaveOpen: true);
            ZipArchiveEntry manifestEntry = FindProjectManifestEntry(archive)
                ?? throw new InvalidDataException("The zip file does not contain an EffectViewer project manifest.");
            List<ZipArchiveEntry> fileEntries = archive.Entries
                .Where(entry => !string.IsNullOrWhiteSpace(entry.Name))
                .ToList();

            ProjectManifest manifest;
            await using (Stream manifestStream = manifestEntry.Open())
            {
                manifest = await JsonSerializer.DeserializeAsync<ProjectManifest>(manifestStream, SerializerOptions)
                    ?? new ProjectManifest();
            }

            Normalize(manifest);
            string projectDirectory = CreateUniqueProjectDirectory(manifest.Name);
            bool importCompleted = false;
            try
            {
                int completed = 0;
                foreach (ZipArchiveEntry entry in fileEntries)
                {
                    string relativePath = GetProjectRelativeZipPath(entry, manifestEntry);
                    if (string.IsNullOrWhiteSpace(relativePath))
                    {
                        completed++;
                        continue;
                    }

                    progress?.Report(new ProjectTransferProgress
                    {
                        Operation = "Importing Project",
                        Message = $"Extracting {relativePath}",
                        CompletedItems = completed,
                        TotalItems = fileEntries.Count
                    });

                    string destination = GetSafeDestinationPath(projectDirectory, relativePath);
                    Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                    await using Stream source = entry.Open();
                    await using FileStream target = File.Create(destination);
                    await source.CopyToAsync(target);
                    completed++;
                    progress?.Report(new ProjectTransferProgress
                    {
                        Operation = "Importing Project",
                        Message = $"Extracted {relativePath}",
                        CompletedItems = completed,
                        TotalItems = fileEntries.Count
                    });
                }

                progress?.Report(new ProjectTransferProgress
                {
                    Operation = "Importing Project",
                    Message = "Loading project",
                    CompletedItems = fileEntries.Count,
                    TotalItems = fileEntries.Count
                });

                EffectProject project = await LoadAsync(projectDirectory);
                await SaveAsync(project);
                importCompleted = true;
                return project;
            }
            finally
            {
                if (!importCompleted && Directory.Exists(projectDirectory))
                {
                    Directory.Delete(projectDirectory, recursive: true);
                }
            }
        }

        public async Task ExportProjectZipAsync(EffectProject project, Stream outputStream, IProgress<ProjectTransferProgress> progress = null)
        {
            if (project is null)
            {
                throw new ArgumentNullException(nameof(project));
            }

            if (outputStream is null)
            {
                throw new ArgumentNullException(nameof(outputStream));
            }

            if (string.IsNullOrWhiteSpace(project.RootPath) || !Directory.Exists(project.RootPath))
            {
                throw new InvalidOperationException("Only projects stored in the app private project folder can be exported.");
            }

            progress?.Report(new ProjectTransferProgress
            {
                Operation = "Exporting Project",
                Message = "Saving project"
            });

            await SaveAsync(project);

            List<string> files = Directory.EnumerateFiles(project.RootPath, "*", SearchOption.AllDirectories)
                .ToList();
            using ZipArchive archive = new(outputStream, ZipArchiveMode.Create, leaveOpen: true);
            int completed = 0;
            foreach (string file in files)
            {
                string relativePath = Path.GetRelativePath(project.RootPath, file).Replace('\\', '/');
                if (string.IsNullOrWhiteSpace(relativePath))
                {
                    completed++;
                    continue;
                }

                progress?.Report(new ProjectTransferProgress
                {
                    Operation = "Exporting Project",
                    Message = $"Compressing {relativePath}",
                    CompletedItems = completed,
                    TotalItems = files.Count
                });

                ZipArchiveEntry entry = archive.CreateEntry(relativePath, CompressionLevel.Optimal);
                await using Stream entryStream = entry.Open();
                await using FileStream source = File.OpenRead(file);
                await source.CopyToAsync(entryStream);
                completed++;
                progress?.Report(new ProjectTransferProgress
                {
                    Operation = "Exporting Project",
                    Message = $"Compressed {relativePath}",
                    CompletedItems = completed,
                    TotalItems = files.Count
                });
            }

            progress?.Report(new ProjectTransferProgress
            {
                Operation = "Exporting Project",
                Message = "Writing archive",
                CompletedItems = files.Count,
                TotalItems = files.Count
            });
        }

        private string CreateUniqueProjectDirectory(string sourceName)
        {
            string baseName = ProjectPathUtility.CreateSafeName(sourceName, "project");
            string projectsRoot = _storageProvider.ProjectsRootPath;
            if (string.IsNullOrWhiteSpace(projectsRoot))
            {
                projectsRoot = new DefaultProjectStorageProvider().ProjectsRootPath;
            }

            Directory.CreateDirectory(projectsRoot);

            string candidate = Path.Combine(projectsRoot, baseName);
            if (!Directory.Exists(candidate) && !File.Exists(Path.Combine(candidate, ManifestFileName)))
            {
                return candidate;
            }

            for (int i = 2; ; i++)
            {
                candidate = Path.Combine(projectsRoot, $"{baseName}-{i}");
                if (!Directory.Exists(candidate) && !File.Exists(Path.Combine(candidate, ManifestFileName)))
                {
                    return candidate;
                }
            }
        }

        private static ZipArchiveEntry FindProjectManifestEntry(ZipArchive archive)
        {
            ZipArchiveEntry rootManifest = archive.Entries.FirstOrDefault(entry =>
                string.Equals(entry.FullName.Replace('\\', '/'), ManifestFileName, StringComparison.OrdinalIgnoreCase));
            if (rootManifest is not null)
            {
                return rootManifest;
            }

            return archive.Entries.FirstOrDefault(entry =>
                string.Equals(Path.GetFileName(entry.FullName), ManifestFileName, StringComparison.OrdinalIgnoreCase));
        }

        private static string GetProjectRelativeZipPath(ZipArchiveEntry entry, ZipArchiveEntry manifestEntry)
        {
            string entryName = entry.FullName.Replace('\\', '/').TrimStart('/');
            string manifestName = manifestEntry.FullName.Replace('\\', '/').TrimStart('/');
            string manifestDirectory = Path.GetDirectoryName(manifestName)?.Replace('\\', '/') ?? string.Empty;
            if (string.IsNullOrWhiteSpace(manifestDirectory))
            {
                return entryName;
            }

            string prefix = manifestDirectory.TrimEnd('/') + "/";
            return entryName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                ? entryName[prefix.Length..]
                : string.Empty;
        }

        private static string GetSafeDestinationPath(string projectDirectory, string relativePath)
        {
            if (Path.IsPathRooted(relativePath) ||
                relativePath.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries).Any(part => part == ".."))
            {
                throw new InvalidDataException("The zip file contains an invalid project path.");
            }

            string normalizedRelativePath = relativePath.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
            string destination = Path.GetFullPath(Path.Combine(projectDirectory, normalizedRelativePath));
            string projectRoot = Path.GetFullPath(projectDirectory);
            if (!destination.StartsWith(projectRoot.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(destination, projectRoot, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("The zip file contains an invalid project path.");
            }

            return destination;
        }

        private static void Normalize(ProjectManifest manifest)
        {
            manifest.Version = manifest.Version <= 0 ? 1 : manifest.Version;
            foreach (ImageAsset image in manifest.Images)
            {
                image.Rows = image.Rows < 1 ? 1 : image.Rows;
                image.Cols = image.Cols < 1 ? 1 : image.Cols;
            }
        }
    }
}
