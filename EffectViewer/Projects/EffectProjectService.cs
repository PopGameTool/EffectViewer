using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using EffectViewer.Assets;
using EffectViewer.TodLib.Particle;
using EffectViewer.TodLib.Reanim;
using EffectViewer.TodLib.Trail;

namespace EffectViewer.Projects
{
    public sealed class EffectProjectService
    {
        public const string ManifestFileName = "project.effectproj.json";
        public const string AssetsDirectoryName = "assets";
        private const string ImagesDirectory = "assets/images";
        private const string ReanimsDirectory = "assets/reanims";
        private const string ParticlesDirectory = "assets/particles";
        private const string TrailsDirectory = "assets/trails";
        private const string ScriptsDirectory = "scripts";

        private readonly IProjectStorageProvider _storageProvider;

        public EffectProjectService(IProjectStorageProvider storageProvider)
        {
            _storageProvider = storageProvider ?? new DefaultProjectStorageProvider();
        }

        public async Task<EffectProject> LoadAsync(string projectDirectory)
        {
            string manifestPath = System.IO.Path.Combine(projectDirectory, ManifestFileName);
            await using FileStream stream = File.OpenRead(manifestPath);
            ProjectManifest manifest = await JsonSerializer.DeserializeAsync(stream, ProjectJsonSerializerContext.Default.ProjectManifest)
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
                    ProjectManifest manifest = await JsonSerializer.DeserializeAsync(stream, ProjectJsonSerializerContext.Default.ProjectManifest)
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
            await JsonSerializer.SerializeAsync(stream, project.Manifest, ProjectJsonSerializerContext.Default.ProjectManifest);
        }

        public EffectProject CreateNew(string projectDirectory, string projectName)
        {
            ProjectManifest manifest = new()
            {
                Name = string.IsNullOrWhiteSpace(projectName) ? "Untitled Effect Project" : projectName
            };

            return new EffectProject(projectDirectory, manifest);
        }

        public async Task<EffectProject> CreateProjectAsync(string projectName)
        {
            string safeName = ProjectPathUtility.CreateSafeName(projectName, "untitled-project");
            string projectDirectory = CreateUniqueProjectDirectory(safeName);
            EffectProject project = CreateNew(
                projectDirectory,
                string.IsNullOrWhiteSpace(projectName) ? "Untitled Effect Project" : projectName.Trim());
            await SaveAsync(project);
            return await LoadAsync(projectDirectory);
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
                    new ReanimAsset { Id = "sample_reanim", Path = "reanim/sample.reanim" }
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

            using MemoryStream archiveBuffer = new();
            await zipStream.CopyToAsync(archiveBuffer);
            archiveBuffer.Position = 0;

            using ZipArchive archive = new(archiveBuffer, ZipArchiveMode.Read);
            ZipArchiveEntry manifestEntry = FindProjectManifestEntry(archive)
                ?? throw new InvalidDataException("The zip file does not contain an EffectViewer project manifest.");
            List<ZipArchiveEntry> fileEntries = archive.Entries
                .Where(entry => !string.IsNullOrWhiteSpace(entry.Name))
                .ToList();

            ProjectManifest manifest;
            await using (Stream manifestStream = manifestEntry.Open())
            {
                manifest = await JsonSerializer.DeserializeAsync(manifestStream, ProjectJsonSerializerContext.Default.ProjectManifest)
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

        public async Task ExportProjectFileAsync(EffectProject project, string projectPath, Stream outputStream)
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
                throw new InvalidOperationException("Only projects stored in the app private project folder can export files.");
            }

            string sourcePath = ResolveProjectFilePath(project, projectPath);
            if (!File.Exists(sourcePath))
            {
                throw new FileNotFoundException("The current project file could not be found.", projectPath);
            }

            await using FileStream source = File.OpenRead(sourcePath);
            await source.CopyToAsync(outputStream);
        }

        public async Task<ProjectResourceResult> ImportResourceFileAsync(
            EffectProject project,
            string sourceFileName,
            Stream sourceStream)
        {
            EnsureWritableProject(project);
            if (sourceStream is null)
            {
                throw new ArgumentNullException(nameof(sourceStream));
            }

            EffectAssetKind kind = DetectResourceKind(sourceFileName);
            string baseName = GetResourceBaseName(sourceFileName, kind);
            string requestedAssetId = kind == EffectAssetKind.Image
                ? CreateImageAssetId(baseName)
                : baseName;
            string assetId = CreateUniqueAssetId(project.Manifest, kind, requestedAssetId);
            string suffix = GetResourceFileSuffix(sourceFileName, kind);
            string relativePath = CreateUniqueAssetPath(project, GetResourceDirectory(kind), assetId, suffix);
            string destinationPath = ResolveProjectFilePath(project, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);

            await using (FileStream target = File.Create(destinationPath))
            {
                await sourceStream.CopyToAsync(target);
            }

            AddManifestAsset(project.Manifest, kind, assetId, relativePath);
            await SaveAsync(project);
            project.RebuildAssetIndex();
            return new ProjectResourceResult(project, kind, assetId, relativePath);
        }

        public async Task<ProjectResourceResult> CreateResourceAsync(
            EffectProject project,
            EffectAssetKind kind,
            string requestedAssetId)
        {
            EnsureWritableProject(project);
            if (kind == EffectAssetKind.Image)
            {
                throw new InvalidOperationException("New image resources are not supported yet. Import an image file instead.");
            }

            if (kind is not (EffectAssetKind.Reanim or EffectAssetKind.Particle or EffectAssetKind.Trail or EffectAssetKind.Showcase))
            {
                throw new InvalidOperationException("The selected resource type cannot be created.");
            }

            string assetId = CreateUniqueAssetId(project.Manifest, kind, requestedAssetId);
            string relativePath = CreateUniqueAssetPath(project, GetResourceDirectory(kind), assetId, GetNewResourceSuffix(kind));
            string destinationPath = ResolveProjectFilePath(project, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);

            await using (FileStream stream = File.Create(destinationPath))
            {
                await WriteDefaultResourceAsync(kind, assetId, stream, destinationPath);
            }

            AddManifestAsset(project.Manifest, kind, assetId, relativePath);
            await SaveAsync(project);
            project.RebuildAssetIndex();
            return new ProjectResourceResult(project, kind, assetId, relativePath);
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

        private static void EnsureWritableProject(EffectProject project)
        {
            if (project is null || string.IsNullOrWhiteSpace(project.RootPath))
            {
                throw new InvalidOperationException("Create or open an internal project before adding resources.");
            }

            Directory.CreateDirectory(project.RootPath);
        }

        private static EffectAssetKind DetectResourceKind(string fileName)
        {
            string normalized = (fileName ?? string.Empty).Replace('\\', '/');
            string lower = Path.GetFileName(normalized).ToLowerInvariant();
            if (lower.EndsWith(".reanim", StringComparison.OrdinalIgnoreCase) ||
                lower.EndsWith(".reanim.compiled", StringComparison.OrdinalIgnoreCase))
            {
                return EffectAssetKind.Reanim;
            }

            if (lower.EndsWith(".trail", StringComparison.OrdinalIgnoreCase) ||
                lower.EndsWith(".trail.compiled", StringComparison.OrdinalIgnoreCase))
            {
                return EffectAssetKind.Trail;
            }

            if (lower.EndsWith(".xml", StringComparison.OrdinalIgnoreCase) ||
                lower.EndsWith(".xml.compiled", StringComparison.OrdinalIgnoreCase))
            {
                return EffectAssetKind.Particle;
            }

            if (lower.EndsWith(".lua", StringComparison.OrdinalIgnoreCase))
            {
                return EffectAssetKind.Showcase;
            }

            string extension = Path.GetExtension(lower);
            if (extension is ".png" or ".jpg" or ".jpeg" or ".bmp" or ".gif" or ".webp" or ".tga")
            {
                return EffectAssetKind.Image;
            }

            throw new InvalidDataException("Unsupported resource file type.");
        }

        private static string GetResourceBaseName(string fileName, EffectAssetKind kind)
        {
            string name = Path.GetFileName(fileName ?? string.Empty);
            string lower = name.ToLowerInvariant();
            string suffix = GetResourceFileSuffix(lower, kind);
            if (!string.IsNullOrEmpty(suffix) && lower.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                name = name[..^suffix.Length];
            }
            else
            {
                name = Path.GetFileNameWithoutExtension(name);
            }

            return ProjectPathUtility.CreateSafeName(name, kind.ToString().ToLowerInvariant());
        }

        private static string GetResourceFileSuffix(string fileName, EffectAssetKind kind)
        {
            string lower = Path.GetFileName(fileName ?? string.Empty).ToLowerInvariant();
            string[] suffixes = kind switch
            {
                EffectAssetKind.Reanim => [".reanim.compiled", ".reanim"],
                EffectAssetKind.Particle => [".xml.compiled", ".xml"],
                EffectAssetKind.Trail => [".trail.compiled", ".trail"],
                EffectAssetKind.Showcase => [".lua"],
                EffectAssetKind.Image => [],
                _ => []
            };

            foreach (string suffix in suffixes)
            {
                if (lower.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                {
                    return suffix;
                }
            }

            string extension = Path.GetExtension(lower);
            return string.IsNullOrWhiteSpace(extension) ? GetNewResourceSuffix(kind) : extension;
        }

        private static string GetNewResourceSuffix(EffectAssetKind kind)
        {
            return kind switch
            {
                EffectAssetKind.Reanim => ".reanim",
                EffectAssetKind.Particle => ".xml",
                EffectAssetKind.Trail => ".trail",
                EffectAssetKind.Showcase => ".lua",
                _ => string.Empty
            };
        }

        private static string GetResourceDirectory(EffectAssetKind kind)
        {
            return kind switch
            {
                EffectAssetKind.Image => ImagesDirectory,
                EffectAssetKind.Reanim => ReanimsDirectory,
                EffectAssetKind.Particle => ParticlesDirectory,
                EffectAssetKind.Trail => TrailsDirectory,
                EffectAssetKind.Showcase => ScriptsDirectory,
                _ => AssetsDirectoryName
            };
        }

        private static string CreateUniqueAssetId(ProjectManifest manifest, EffectAssetKind kind, string requestedAssetId)
        {
            string baseId = kind == EffectAssetKind.Image
                ? CreateSafeImageAssetId(requestedAssetId)
                : ProjectPathUtility.CreateSafeName(requestedAssetId, kind.ToString().ToLowerInvariant());
            string candidate = baseId;
            for (int i = 2; AssetIdExists(manifest, kind, candidate); i++)
            {
                candidate = $"{baseId}_{i}";
            }

            return candidate;
        }

        public static string CreateImageAssetId(string name)
        {
            return "IMAGE_" + CreateImageIdName(name);
        }

        public static string CreateSafeImageAssetId(string requestedAssetId)
        {
            string id = requestedAssetId ?? string.Empty;
            return id.StartsWith("IMAGE_", StringComparison.OrdinalIgnoreCase)
                ? "IMAGE_" + CreateImageIdName(id[6..])
                : CreateImageAssetId(id);
        }

        private static string CreateImageIdName(string name)
        {
            string safeName = ProjectPathUtility.CreateSafeName(name, "image");
            return safeName.ToUpperInvariant();
        }

        private static bool AssetIdExists(ProjectManifest manifest, EffectAssetKind kind, string assetId)
        {
            StringComparer comparer = StringComparer.OrdinalIgnoreCase;
            return kind switch
            {
                EffectAssetKind.Image => manifest.Images.Any(asset => comparer.Equals(asset.Id, assetId)),
                EffectAssetKind.Reanim => manifest.Reanims.Any(asset => comparer.Equals(asset.Id, assetId)),
                EffectAssetKind.Particle => manifest.Particles.Any(asset => comparer.Equals(asset.Id, assetId)),
                EffectAssetKind.Trail => manifest.Trails.Any(asset => comparer.Equals(asset.Id, assetId)),
                EffectAssetKind.Showcase => manifest.Showcases.Any(asset => comparer.Equals(asset.Id, assetId)),
                _ => false
            };
        }

        private static string CreateUniqueAssetPath(EffectProject project, string directory, string assetId, string suffix)
        {
            string baseName = ProjectPathUtility.CreateSafeName(assetId, "resource");
            string candidate = ProjectPathUtility.ToProjectRelativePath(Path.Combine(directory, baseName + suffix));
            for (int i = 2; File.Exists(ResolveProjectFilePath(project, candidate)); i++)
            {
                candidate = ProjectPathUtility.ToProjectRelativePath(Path.Combine(directory, $"{baseName}_{i}{suffix}"));
            }

            return candidate;
        }

        private static void AddManifestAsset(ProjectManifest manifest, EffectAssetKind kind, string assetId, string relativePath)
        {
            switch (kind)
            {
                case EffectAssetKind.Image:
                    manifest.Images.Add(new ImageAsset { Id = assetId, Path = relativePath, Rows = 1, Cols = 1 });
                    break;

                case EffectAssetKind.Reanim:
                    manifest.Reanims.Add(new ReanimAsset { Id = assetId, Path = relativePath });
                    break;

                case EffectAssetKind.Particle:
                    manifest.Particles.Add(new EffectAsset { Id = assetId, Path = relativePath });
                    break;

                case EffectAssetKind.Trail:
                    manifest.Trails.Add(new EffectAsset { Id = assetId, Path = relativePath });
                    break;

                case EffectAssetKind.Showcase:
                    manifest.Showcases.Add(new ShowcaseAsset { Id = assetId, Path = relativePath });
                    break;

                default:
                    throw new InvalidOperationException("The selected resource type cannot be added.");
            }
        }

        private static async Task WriteDefaultResourceAsync(
            EffectAssetKind kind,
            string assetId,
            Stream stream,
            string targetFileName)
        {
            switch (kind)
            {
                case EffectAssetKind.Reanim:
                    ReanimReader.Encode(stream, CreateDefaultReanimDefinition(), targetFileName);
                    return;

                case EffectAssetKind.Particle:
                    SexyParticleReader.Encode(stream, ParticleDefinitionUtility.CreateEmpty(), targetFileName);
                    return;

                case EffectAssetKind.Trail:
                    TrailDefinition trail = new();
                    trail.ApplyDefaults();
                    TrailReader.Encode(stream, trail, targetFileName);
                    return;

                case EffectAssetKind.Showcase:
                    using (StreamWriter writer = new(stream, leaveOpen: true))
                    {
                        await writer.WriteAsync(CreateDefaultShowcaseScript(assetId));
                    }

                    return;

                default:
                    throw new InvalidOperationException("The selected resource type cannot be created.");
            }
        }

        private static ReanimatorDefinition CreateDefaultReanimDefinition()
        {
            ReanimatorDefinition definition = new()
            {
                mFPS = 12f,
                mTrackCount = 1,
                mTracks =
                [
                    new ReanimatorTrack("track_1", 1)
                ]
            };
            definition.mTracks[0].mTransforms[0] = new ReanimatorTransform
            {
                mTransX = 0f,
                mTransY = 0f,
                mSkewX = 0f,
                mSkewY = 0f,
                mScaleX = 1f,
                mScaleY = 1f,
                mFrame = 0f,
                mAlpha = 1f,
                mImage = null,
                mFont = null,
                mText = string.Empty
            };
            definition.Init();
            return definition;
        }

        private static string CreateDefaultShowcaseScript(string assetId)
        {
            string safeId = string.IsNullOrWhiteSpace(assetId) ? "showcase" : assetId;
            return $"""
                scene.clear()
                effect.log("{safeId} initialized")
                """;
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

        private static string ResolveProjectFilePath(EffectProject project, string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new InvalidOperationException("The current document does not have a project file path.");
            }

            string normalizedPath = path.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
            string sourcePath = Path.IsPathRooted(normalizedPath)
                ? Path.GetFullPath(normalizedPath)
                : Path.GetFullPath(Path.Combine(project.RootPath, normalizedPath));
            string projectRoot = Path.GetFullPath(project.RootPath);
            if (!sourcePath.StartsWith(projectRoot.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(sourcePath, projectRoot, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("The current document path points outside the project folder.");
            }

            return sourcePath;
        }

        private static void Normalize(ProjectManifest manifest)
        {
            manifest.Version = manifest.Version <= 0 ? 1 : manifest.Version;
            manifest.Images ??= [];
            manifest.Reanims ??= [];
            manifest.Particles ??= [];
            manifest.Trails ??= [];
            manifest.Showcases ??= [];

            foreach (ImageAsset image in manifest.Images)
            {
                image.Rows = image.Rows < 1 ? 1 : image.Rows;
                image.Cols = image.Cols < 1 ? 1 : image.Cols;
            }

            foreach (ReanimAsset reanim in manifest.Reanims)
            {
                reanim.Tweens ??= [];
                foreach (ReanimTween tween in reanim.Tweens)
                {
                    tween.TrackIndex = tween.TrackIndex < 0 ? 0 : tween.TrackIndex;
                    tween.TrackName ??= string.Empty;
                    tween.StartFrame = tween.StartFrame < 0 ? 0 : tween.StartFrame;
                    tween.EndFrame = tween.EndFrame < tween.StartFrame ? tween.StartFrame : tween.EndFrame;
                    tween.Properties ??= [];
                }

                reanim.Tweens.RemoveAll(tween => tween.EndFrame <= tween.StartFrame);
            }
        }
    }
}
