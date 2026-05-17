using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using EffectViewer.Assets;
using EffectViewer.EffectRuntime.Particle;
using EffectViewer.EffectRuntime.Reanim;
using EffectViewer.EffectRuntime.Trail;

namespace EffectViewer.Projects
{
    public sealed class EffectProjectService
    {
        public const string ManifestFileName = "project.effectproj.json";
        public const string AssetsDirectoryName = "assets";
        private const string ImagesDirectory = "assets/images";
        private const string FontsDirectory = "assets/fonts";
        private const string ReanimsDirectory = "assets/reanims";
        private const string ParticlesDirectory = "assets/particles";
        private const string TrailsDirectory = "assets/trails";
        private const string ScriptsDirectory = "scripts";

        private readonly IProjectStorageProvider _storageProvider;

        public EffectProjectService(IProjectStorageProvider storageProvider)
        {
            _storageProvider = storageProvider ?? new DefaultProjectStorageProvider();
        }

        public async Task<EffectProject> LoadAsync(
            string projectDirectory,
            IProgress<ProjectTransferProgress> progress = null)
        {
            string manifestPath = System.IO.Path.Combine(projectDirectory, ManifestFileName);
            await using FileStream stream = File.OpenRead(manifestPath);
            ProjectManifest manifest = await JsonSerializer.DeserializeAsync(stream, ProjectJsonSerializerContext.Default.ProjectManifest)
                ?? new ProjectManifest();

            Normalize(manifest);
            EffectProject project = new(projectDirectory, manifest);
            await Task.Run(() => project.Definitions.PreloadAll(progress));
            return project;
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
            await using (FileStream stream = File.Create(manifestPath))
            {
                await JsonSerializer.SerializeAsync(stream, project.Manifest, ProjectJsonSerializerContext.Default.ProjectManifest);
            }

            await FlushStorageAsync();
        }

        public async Task FlushStorageAsync()
        {
            if (_storageProvider is IProjectStoragePersistence persistence)
            {
                await persistence.FlushAsync();
            }
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

        public async Task<EffectProject> RenameProjectAsync(string projectDirectory, string projectName)
        {
            string sourceDirectory = EnsureInternalProjectDirectory(projectDirectory);
            string name = string.IsNullOrWhiteSpace(projectName)
                ? "Untitled Effect Project"
                : projectName.Trim();

            EffectProject project = await LoadAsync(sourceDirectory);
            project.Manifest.Name = name;
            await SaveAsync(project);

            string safeName = ProjectPathUtility.CreateSafeName(name, "untitled-project");
            string targetDirectory = CreateUniqueProjectDirectory(safeName, sourceDirectory);
            if (!PathsEqual(sourceDirectory, targetDirectory))
            {
                Directory.Move(sourceDirectory, targetDirectory);
                await FlushStorageAsync();
            }

            return await LoadAsync(targetDirectory);
        }

        public async Task DeleteProjectAsync(string projectDirectory)
        {
            string sourceDirectory = EnsureInternalProjectDirectory(projectDirectory);
            await Task.Run(() => Directory.Delete(sourceDirectory, recursive: true));
            await FlushStorageAsync();
        }

        public async Task<FolderImportResult> ImportFolderAsync(
            string sourceDirectory,
            IProgress<ProjectTransferProgress> progress = null)
        {
            return await Task.Run(() => ImportFolderAsync(new LocalResourceFolderSource(sourceDirectory), progress));
        }

        public async Task<FolderImportResult> ImportFolderAsync(
            EffectProject project,
            string sourceDirectory,
            IProgress<ProjectTransferProgress> progress = null,
            ImportConflictResolver conflictResolver = null)
        {
            EnsureWritableProject(project);
            if (string.IsNullOrWhiteSpace(sourceDirectory))
            {
                throw new ArgumentException("A source directory is required when importing assets.", nameof(sourceDirectory));
            }

            return await ImportFolderAsync(project, new LocalResourceFolderSource(sourceDirectory), progress, conflictResolver);
        }

        public async Task<FolderImportResult> ImportFolderAsync(
            IResourceFolderSource source,
            IProgress<ProjectTransferProgress> progress = null)
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            string projectDirectory = CreateUniqueProjectDirectory(source.Name);
            ResourceFolderImporter importer = new();
            FolderImportResult result = await importer.ImportAsync(source, projectDirectory, progress);
            await SaveAsync(result.Project);
            await Task.Run(() => result.Project.Definitions.PreloadAll());

            return result;
        }

        public async Task<FolderImportResult> ImportFolderAsync(
            EffectProject project,
            IResourceFolderSource source,
            IProgress<ProjectTransferProgress> progress = null,
            ImportConflictResolver conflictResolver = null)
        {
            EnsureWritableProject(project);
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            string importDirectory = Path.Combine(Path.GetTempPath(), "EffectViewer", "folder-import-" + Guid.NewGuid().ToString("N"));
            try
            {
                ResourceFolderImporter importer = new();
                FolderImportResult imported = await importer.ImportAsync(source, importDirectory, progress);
                FolderImportResult result = await MergeImportedProjectAsync(project, imported.Project, imported.MissingImageCount, conflictResolver);
                await SaveAsync(project);
                project.RebuildAssetIndex();
                project.Definitions.PreloadAll();
                return result;
            }
            finally
            {
                TryDeleteDirectory(importDirectory);
            }
        }

        public async Task<FolderImportResult> ImportFolderAsync(
            IStorageFolder sourceFolder,
            IProgress<ProjectTransferProgress> progress = null)
        {
            using StorageResourceFolderSource source = new(sourceFolder);
            return await ImportFolderAsync(source, progress);
        }

        public async Task<FolderImportResult> ImportFolderAsync(
            EffectProject project,
            IStorageFolder sourceFolder,
            IProgress<ProjectTransferProgress> progress = null,
            ImportConflictResolver conflictResolver = null)
        {
            EnsureWritableProject(project);
            if (sourceFolder is null)
            {
                throw new ArgumentNullException(nameof(sourceFolder));
            }

            using StorageResourceFolderSource source = new(sourceFolder);
            return await ImportFolderAsync(project, source, progress, conflictResolver);
        }

        public async Task<FolderImportResult> ImportPakAsync(
            string sourceFileName,
            Stream pakStream,
            IProgress<ProjectTransferProgress> progress = null)
        {
            PakResourceFolderSource source = await PakResourceFolderSource.FromStreamAsync(sourceFileName, pakStream);
            return await ImportFolderAsync(source, progress);
        }

        public async Task<FolderImportResult> ImportPakAsync(
            EffectProject project,
            string sourceFileName,
            Stream pakStream,
            IProgress<ProjectTransferProgress> progress = null,
            ImportConflictResolver conflictResolver = null)
        {
            EnsureWritableProject(project);
            PakResourceFolderSource source = await PakResourceFolderSource.FromStreamAsync(sourceFileName, pakStream);
            return await ImportFolderAsync(project, source, progress, conflictResolver);
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

                EffectProject project = await LoadAsync(projectDirectory, progress);
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
            project.Definitions.Invalidate(kind, relativePath);
            project.Definitions.PreloadAll();
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
            project.Definitions.Invalidate(kind, relativePath);
            project.Definitions.PreloadAll();
            return new ProjectResourceResult(project, kind, assetId, relativePath);
        }

        public async Task<ProjectResourceResult> DeleteResourceAsync(
            EffectProject project,
            EffectAssetKind kind,
            string assetId,
            string projectPath)
        {
            EnsureWritableProject(project);

            string deletedAssetId;
            string deletedProjectPath;
            List<string> deletedPaths = [];
            Action removeAsset;
            switch (kind)
            {
                case EffectAssetKind.Image:
                    int imageIndex = project.Manifest.Images.FindIndex(asset => AssetMatches(asset.Id, asset.Path, assetId, projectPath));
                    if (imageIndex < 0)
                    {
                        throw new InvalidOperationException("The selected resource could not be found.");
                    }

                    ImageAsset image = project.Manifest.Images[imageIndex];
                    deletedAssetId = image.Id;
                    deletedProjectPath = image.Path;
                    AddProjectPath(deletedPaths, image.Path);
                    AddProjectPath(deletedPaths, image.AlphaPath);
                    removeAsset = () => project.Manifest.Images.RemoveAt(imageIndex);
                    break;

                case EffectAssetKind.Font:
                    int fontIndex = FindManifestAssetIndex(project.Manifest.Fonts, assetId, projectPath);
                    FontAsset font = project.Manifest.Fonts[fontIndex];
                    deletedAssetId = font.Id;
                    deletedProjectPath = font.Path;
                    AddProjectPath(deletedPaths, font.Path);
                    removeAsset = () => project.Manifest.Fonts.RemoveAt(fontIndex);
                    break;

                case EffectAssetKind.Reanim:
                    int reanimIndex = FindManifestAssetIndex(project.Manifest.Reanims, assetId, projectPath);
                    ReanimAsset reanim = project.Manifest.Reanims[reanimIndex];
                    deletedAssetId = reanim.Id;
                    deletedProjectPath = reanim.Path;
                    AddProjectPath(deletedPaths, reanim.Path);
                    removeAsset = () => project.Manifest.Reanims.RemoveAt(reanimIndex);
                    break;

                case EffectAssetKind.Particle:
                    int particleIndex = FindManifestAssetIndex(project.Manifest.Particles, assetId, projectPath);
                    EffectAsset particle = project.Manifest.Particles[particleIndex];
                    deletedAssetId = particle.Id;
                    deletedProjectPath = particle.Path;
                    AddProjectPath(deletedPaths, particle.Path);
                    removeAsset = () => project.Manifest.Particles.RemoveAt(particleIndex);
                    break;

                case EffectAssetKind.Trail:
                    int trailIndex = FindManifestAssetIndex(project.Manifest.Trails, assetId, projectPath);
                    EffectAsset trail = project.Manifest.Trails[trailIndex];
                    deletedAssetId = trail.Id;
                    deletedProjectPath = trail.Path;
                    AddProjectPath(deletedPaths, trail.Path);
                    removeAsset = () => project.Manifest.Trails.RemoveAt(trailIndex);
                    break;

                case EffectAssetKind.Showcase:
                    int showcaseIndex = project.Manifest.Showcases.FindIndex(asset => AssetMatches(asset.Id, asset.Path, assetId, projectPath));
                    if (showcaseIndex < 0)
                    {
                        throw new InvalidOperationException("The selected resource could not be found.");
                    }

                    ShowcaseAsset showcase = project.Manifest.Showcases[showcaseIndex];
                    deletedAssetId = showcase.Id;
                    deletedProjectPath = showcase.Path;
                    AddProjectPath(deletedPaths, showcase.Path);
                    removeAsset = () => project.Manifest.Showcases.RemoveAt(showcaseIndex);
                    break;

                default:
                    throw new InvalidOperationException("The selected resource type cannot be deleted.");
            }

            foreach (string path in deletedPaths.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                DeleteProjectFileIfExists(project, path);
            }

            removeAsset();
            await SaveAsync(project);
            project.RebuildAssetIndex();
            project.Definitions.Invalidate(kind, deletedProjectPath);
            return new ProjectResourceResult(project, kind, deletedAssetId, deletedProjectPath);
        }

        private async Task<FolderImportResult> MergeImportedProjectAsync(
            EffectProject project,
            EffectProject importedProject,
            int missingImageCount,
            ImportConflictResolver conflictResolver)
        {
            Dictionary<string, string> imageIdMap = new(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, string> fontIdMap = new(StringComparer.OrdinalIgnoreCase);

            int imageCount = await MergeImportedImagesAsync(project, importedProject, imageIdMap, conflictResolver);
            int fontCount = await MergeImportedFontsAsync(project, importedProject, imageIdMap, fontIdMap, conflictResolver);
            int reanimCount = await MergeImportedReanimsAsync(project, importedProject, imageIdMap, fontIdMap, conflictResolver);
            int particleCount = await MergeImportedEffectAssetsAsync(
                project,
                importedProject,
                importedProject.Manifest.Particles,
                project.Manifest.Particles,
                EffectAssetKind.Particle,
                imageIdMap,
                conflictResolver,
                CopyImportedParticleFileAsync);
            int trailCount = await MergeImportedEffectAssetsAsync(
                project,
                importedProject,
                importedProject.Manifest.Trails,
                project.Manifest.Trails,
                EffectAssetKind.Trail,
                imageIdMap,
                conflictResolver,
                CopyImportedTrailFileAsync);

            return new FolderImportResult(project, imageCount, fontCount, reanimCount, particleCount, trailCount, missingImageCount);
        }

        private static async Task<int> MergeImportedImagesAsync(
            EffectProject project,
            EffectProject importedProject,
            Dictionary<string, string> imageIdMap,
            ImportConflictResolver conflictResolver)
        {
            int importedCount = 0;
            foreach (ImageAsset incoming in importedProject.Manifest.Images)
            {
                ImportConflictResolution? conflict = await ResolveImportConflictAsync(project.Manifest, EffectAssetKind.Image, incoming.Id, incoming.Path, conflictResolver);
                if (conflict == ImportConflictResolution.Skip)
                {
                    imageIdMap[incoming.Id] = incoming.Id;
                    continue;
                }

                if (conflict == ImportConflictResolution.Overwrite &&
                    TryFindImageAsset(project.Manifest, incoming.Id, out ImageAsset existingImage))
                {
                    string path = await CopyImportedAssetFileAsync(
                        importedProject,
                        incoming.Path,
                        project,
                        existingImage.Path,
                        ImagesDirectory,
                        existingImage.Id);
                    string alphaPath = await CopyImportedAssetFileAsync(
                        importedProject,
                        incoming.AlphaPath,
                        project,
                        existingImage.AlphaPath,
                        ImagesDirectory,
                        existingImage.Id + ".alpha");

                    existingImage.Path = path;
                    existingImage.AlphaPath = alphaPath;
                    existingImage.AlphaOnly = incoming.AlphaOnly;
                    existingImage.Rows = incoming.Rows;
                    existingImage.Cols = incoming.Cols;
                    imageIdMap[incoming.Id] = existingImage.Id;
                    importedCount++;
                    continue;
                }

                string assetId = CreateUniqueAssetId(project.Manifest, EffectAssetKind.Image, incoming.Id);
                string targetPath = await CopyImportedAssetFileAsync(importedProject, incoming.Path, project, null, ImagesDirectory, assetId);
                string targetAlphaPath = await CopyImportedAssetFileAsync(importedProject, incoming.AlphaPath, project, null, ImagesDirectory, assetId + ".alpha");

                project.Manifest.Images.Add(new ImageAsset
                {
                    Id = assetId,
                    Path = targetPath,
                    AlphaPath = targetAlphaPath,
                    AlphaOnly = incoming.AlphaOnly,
                    Rows = incoming.Rows,
                    Cols = incoming.Cols
                });
                imageIdMap[incoming.Id] = assetId;
                importedCount++;
            }

            return importedCount;
        }

        private static async Task<int> MergeImportedFontsAsync(
            EffectProject project,
            EffectProject importedProject,
            IReadOnlyDictionary<string, string> imageIdMap,
            Dictionary<string, string> fontIdMap,
            ImportConflictResolver conflictResolver)
        {
            int importedCount = 0;
            foreach (FontAsset incoming in importedProject.Manifest.Fonts)
            {
                ImportConflictResolution? conflict = await ResolveImportConflictAsync(project.Manifest, EffectAssetKind.Font, incoming.Id, incoming.Path, conflictResolver);
                if (conflict == ImportConflictResolution.Skip)
                {
                    fontIdMap[incoming.Id] = incoming.Id;
                    continue;
                }

                if (conflict == ImportConflictResolution.Overwrite &&
                    TryFindAsset(project.Manifest.Fonts, incoming.Id, out FontAsset existingFont))
                {
                    existingFont.Path = await CopyImportedFontFileAsync(importedProject, incoming.Path, project, existingFont.Path, existingFont.Id, imageIdMap);
                    existingFont.TrueType = incoming.TrueType;
                    existingFont.FontSize = incoming.FontSize;
                    existingFont.BorderSize = incoming.BorderSize;
                    fontIdMap[incoming.Id] = existingFont.Id;
                    importedCount++;
                    continue;
                }

                string assetId = CreateUniqueAssetId(project.Manifest, EffectAssetKind.Font, incoming.Id);
                string targetPath = await CopyImportedFontFileAsync(importedProject, incoming.Path, project, null, assetId, imageIdMap);
                project.Manifest.Fonts.Add(new FontAsset
                {
                    Id = assetId,
                    Path = targetPath,
                    TrueType = incoming.TrueType,
                    FontSize = incoming.FontSize,
                    BorderSize = incoming.BorderSize
                });
                fontIdMap[incoming.Id] = assetId;
                importedCount++;
            }

            return importedCount;
        }

        private static async Task<int> MergeImportedReanimsAsync(
            EffectProject project,
            EffectProject importedProject,
            IReadOnlyDictionary<string, string> imageIdMap,
            IReadOnlyDictionary<string, string> fontIdMap,
            ImportConflictResolver conflictResolver)
        {
            int importedCount = 0;
            foreach (ReanimAsset incoming in importedProject.Manifest.Reanims)
            {
                ImportConflictResolution? conflict = await ResolveImportConflictAsync(project.Manifest, EffectAssetKind.Reanim, incoming.Id, incoming.Path, conflictResolver);
                if (conflict == ImportConflictResolution.Skip)
                {
                    continue;
                }

                if (conflict == ImportConflictResolution.Overwrite &&
                    TryFindAsset(project.Manifest.Reanims, incoming.Id, out ReanimAsset existingReanim))
                {
                    existingReanim.Path = await CopyImportedReanimFileAsync(importedProject, incoming.Path, project, existingReanim.Path, existingReanim.Id, imageIdMap, fontIdMap);
                    existingReanim.Tweens = CloneTweens(incoming.Tweens);
                    importedCount++;
                    continue;
                }

                string assetId = CreateUniqueAssetId(project.Manifest, EffectAssetKind.Reanim, incoming.Id);
                string targetPath = await CopyImportedReanimFileAsync(importedProject, incoming.Path, project, null, assetId, imageIdMap, fontIdMap);
                project.Manifest.Reanims.Add(new ReanimAsset
                {
                    Id = assetId,
                    Path = targetPath,
                    Tweens = CloneTweens(incoming.Tweens)
                });
                importedCount++;
            }

            return importedCount;
        }

        private static async Task<int> MergeImportedEffectAssetsAsync(
            EffectProject project,
            EffectProject importedProject,
            IEnumerable<EffectAsset> incomingAssets,
            List<EffectAsset> targetAssets,
            EffectAssetKind kind,
            IReadOnlyDictionary<string, string> imageIdMap,
            ImportConflictResolver conflictResolver,
            Func<EffectProject, string, EffectProject, string, string, IReadOnlyDictionary<string, string>, Task<string>> copyFileAsync)
        {
            int importedCount = 0;
            foreach (EffectAsset incoming in incomingAssets)
            {
                ImportConflictResolution? conflict = await ResolveImportConflictAsync(project.Manifest, kind, incoming.Id, incoming.Path, conflictResolver);
                if (conflict == ImportConflictResolution.Skip)
                {
                    continue;
                }

                if (conflict == ImportConflictResolution.Overwrite &&
                    TryFindAsset(targetAssets, incoming.Id, out EffectAsset existingAsset))
                {
                    existingAsset.Path = await copyFileAsync(importedProject, incoming.Path, project, existingAsset.Path, existingAsset.Id, imageIdMap);
                    importedCount++;
                    continue;
                }

                string assetId = CreateUniqueAssetId(project.Manifest, kind, incoming.Id);
                string targetPath = await copyFileAsync(importedProject, incoming.Path, project, null, assetId, imageIdMap);
                targetAssets.Add(new EffectAsset
                {
                    Id = assetId,
                    Path = targetPath
                });
                importedCount++;
            }

            return importedCount;
        }

        private static async Task<ImportConflictResolution?> ResolveImportConflictAsync(
            ProjectManifest manifest,
            EffectAssetKind kind,
            string assetId,
            string incomingPath,
            ImportConflictResolver conflictResolver)
        {
            if (!AssetIdExists(manifest, kind, assetId))
            {
                return null;
            }

            if (conflictResolver is null)
            {
                return ImportConflictResolution.KeepBoth;
            }

            return await conflictResolver(new ImportAssetConflict
            {
                Kind = kind,
                AssetId = assetId,
                ExistingProjectPath = GetExistingAssetPath(manifest, kind, assetId),
                IncomingProjectPath = incomingPath
            });
        }

        private static async Task<string> CopyImportedAssetFileAsync(
            EffectProject importedProject,
            string sourcePath,
            EffectProject project,
            string overwritePath,
            string assetDirectory,
            string assetId)
        {
            if (string.IsNullOrWhiteSpace(sourcePath))
            {
                return string.Empty;
            }

            string targetPath = CreateImportTargetPath(project, overwritePath, assetDirectory, assetId, Path.GetExtension(sourcePath));
            await CopyProjectFileAsync(importedProject, sourcePath, project, targetPath);
            return targetPath;
        }

        private static async Task<string> CopyImportedFontFileAsync(
            EffectProject importedProject,
            string sourcePath,
            EffectProject project,
            string overwritePath,
            string assetId,
            IReadOnlyDictionary<string, string> imageIdMap)
        {
            if (string.IsNullOrWhiteSpace(sourcePath))
            {
                return string.Empty;
            }

            string targetPath = CreateImportTargetPath(project, overwritePath, FontsDirectory, assetId, Path.GetExtension(sourcePath));
            await CopyProjectFileWithResourceIdRemapAsync(importedProject, sourcePath, project, targetPath, imageIdMap, null);
            return targetPath;
        }

        private static async Task<string> CopyImportedReanimFileAsync(
            EffectProject importedProject,
            string sourcePath,
            EffectProject project,
            string overwritePath,
            string assetId,
            IReadOnlyDictionary<string, string> imageIdMap,
            IReadOnlyDictionary<string, string> fontIdMap)
        {
            if (string.IsNullOrWhiteSpace(sourcePath))
            {
                return string.Empty;
            }

            string targetPath = CreateImportTargetPath(project, overwritePath, ReanimsDirectory, assetId, ".reanim");
            try
            {
                string sourceFullPath = ResolveProjectFilePath(importedProject, sourcePath);
                string targetFullPath = ResolveProjectFilePath(project, targetPath);
                Directory.CreateDirectory(Path.GetDirectoryName(targetFullPath)!);

                await using FileStream input = File.OpenRead(sourceFullPath);
                ReanimatorDefinition definition = ReanimReader.Decode(input);
                RemapReanimResourceIds(definition, imageIdMap, fontIdMap);

                await using FileStream output = File.Create(targetFullPath);
                ReanimReader.WriteXml(output, definition);
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or FormatException)
            {
                await CopyProjectFileWithResourceIdRemapAsync(importedProject, sourcePath, project, targetPath, imageIdMap, fontIdMap);
            }

            return targetPath;
        }

        private static async Task<string> CopyImportedParticleFileAsync(
            EffectProject importedProject,
            string sourcePath,
            EffectProject project,
            string overwritePath,
            string assetId,
            IReadOnlyDictionary<string, string> imageIdMap)
        {
            if (string.IsNullOrWhiteSpace(sourcePath))
            {
                return string.Empty;
            }

            string targetPath = CreateImportTargetPath(project, overwritePath, ParticlesDirectory, assetId, ".xml");
            try
            {
                string sourceFullPath = ResolveProjectFilePath(importedProject, sourcePath);
                string targetFullPath = ResolveProjectFilePath(project, targetPath);
                Directory.CreateDirectory(Path.GetDirectoryName(targetFullPath)!);

                await using FileStream input = File.OpenRead(sourceFullPath);
                ParticleDefinition definition = ParticleDefinitionCodec.Decode(input);
                RemapParticleResourceIds(definition, imageIdMap);

                await using FileStream output = File.Create(targetFullPath);
                ParticleDefinitionCodec.WriteXml(output, definition);
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or FormatException)
            {
                await CopyProjectFileWithResourceIdRemapAsync(importedProject, sourcePath, project, targetPath, imageIdMap, null);
            }

            return targetPath;
        }

        private static async Task<string> CopyImportedTrailFileAsync(
            EffectProject importedProject,
            string sourcePath,
            EffectProject project,
            string overwritePath,
            string assetId,
            IReadOnlyDictionary<string, string> imageIdMap)
        {
            if (string.IsNullOrWhiteSpace(sourcePath))
            {
                return string.Empty;
            }

            string targetPath = CreateImportTargetPath(project, overwritePath, TrailsDirectory, assetId, ".trail");
            try
            {
                string sourceFullPath = ResolveProjectFilePath(importedProject, sourcePath);
                string targetFullPath = ResolveProjectFilePath(project, targetPath);
                Directory.CreateDirectory(Path.GetDirectoryName(targetFullPath)!);

                await using FileStream input = File.OpenRead(sourceFullPath);
                TrailDefinition definition = TrailReader.Decode(input);
                definition.mImage = RemapResourceId(definition.mImage, imageIdMap);

                await using FileStream output = File.Create(targetFullPath);
                TrailReader.WriteXml(output, definition);
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or FormatException)
            {
                await CopyProjectFileWithResourceIdRemapAsync(importedProject, sourcePath, project, targetPath, imageIdMap, null);
            }

            return targetPath;
        }

        private static string CreateImportTargetPath(
            EffectProject project,
            string overwritePath,
            string assetDirectory,
            string assetId,
            string suffix)
        {
            suffix ??= string.Empty;
            if (!string.IsNullOrWhiteSpace(overwritePath) &&
                string.Equals(Path.GetExtension(overwritePath), suffix, StringComparison.OrdinalIgnoreCase))
            {
                return overwritePath;
            }

            return CreateUniqueAssetPath(project, assetDirectory, assetId, suffix);
        }

        private static async Task CopyProjectFileAsync(
            EffectProject sourceProject,
            string sourcePath,
            EffectProject targetProject,
            string targetPath)
        {
            string sourceFullPath = ResolveProjectFilePath(sourceProject, sourcePath);
            string targetFullPath = ResolveProjectFilePath(targetProject, targetPath);
            Directory.CreateDirectory(Path.GetDirectoryName(targetFullPath)!);

            await using FileStream input = File.OpenRead(sourceFullPath);
            await using FileStream output = File.Create(targetFullPath);
            await input.CopyToAsync(output);
        }

        private static async Task CopyProjectFileWithResourceIdRemapAsync(
            EffectProject sourceProject,
            string sourcePath,
            EffectProject targetProject,
            string targetPath,
            IReadOnlyDictionary<string, string> imageIdMap,
            IReadOnlyDictionary<string, string> fontIdMap)
        {
            if (!IsTextResourcePath(sourcePath) || (!HasChangedResourceIds(imageIdMap) && !HasChangedResourceIds(fontIdMap)))
            {
                await CopyProjectFileAsync(sourceProject, sourcePath, targetProject, targetPath);
                return;
            }

            string sourceFullPath = ResolveProjectFilePath(sourceProject, sourcePath);
            string targetFullPath = ResolveProjectFilePath(targetProject, targetPath);
            Directory.CreateDirectory(Path.GetDirectoryName(targetFullPath)!);

            string text = await File.ReadAllTextAsync(sourceFullPath);
            text = ReplaceResourceIds(text, imageIdMap);
            text = ReplaceResourceIds(text, fontIdMap);
            await File.WriteAllTextAsync(targetFullPath, text);
        }

        private static void RemapReanimResourceIds(
            ReanimatorDefinition definition,
            IReadOnlyDictionary<string, string> imageIdMap,
            IReadOnlyDictionary<string, string> fontIdMap)
        {
            if (definition?.mTracks is null)
            {
                return;
            }

            int trackCount = Math.Min(definition.mTrackCount, definition.mTracks.Length);
            for (int trackIndex = 0; trackIndex < trackCount; trackIndex++)
            {
                ReanimatorTrack track = definition.mTracks[trackIndex];
                if (track?.mTransforms is null)
                {
                    continue;
                }

                int transformCount = Math.Min(track.mTransformCount, track.mTransforms.Length);
                for (int transformIndex = 0; transformIndex < transformCount; transformIndex++)
                {
                    ReanimatorTransform transform = track.mTransforms[transformIndex];
                    transform.mImage = RemapResourceId(transform.mImage, imageIdMap);
                    transform.mFont = RemapResourceId(transform.mFont, fontIdMap);
                    track.mTransforms[transformIndex] = transform;
                }
            }
        }

        private static void RemapParticleResourceIds(
            ParticleDefinition definition,
            IReadOnlyDictionary<string, string> imageIdMap)
        {
            if (definition?.mEmitterDefs is null)
            {
                return;
            }

            int emitterCount = Math.Min(definition.mEmitterDefCount, definition.mEmitterDefs.Length);
            for (int i = 0; i < emitterCount; i++)
            {
                if (definition.mEmitterDefs[i] is not null)
                {
                    definition.mEmitterDefs[i].mImage = RemapResourceId(definition.mEmitterDefs[i].mImage, imageIdMap);
                }
            }
        }

        private static string RemapResourceId(string id, IReadOnlyDictionary<string, string> idMap)
        {
            return !string.IsNullOrWhiteSpace(id) && idMap is not null && idMap.TryGetValue(id, out string mappedId)
                ? mappedId
                : id;
        }

        private static string ReplaceResourceIds(string text, IReadOnlyDictionary<string, string> idMap)
        {
            if (string.IsNullOrEmpty(text) || idMap is null)
            {
                return text;
            }

            foreach ((string sourceId, string targetId) in idMap.Where(item => !string.Equals(item.Key, item.Value, StringComparison.OrdinalIgnoreCase)))
            {
                text = text.Replace(sourceId, targetId, StringComparison.OrdinalIgnoreCase);
            }

            return text;
        }

        private static bool HasChangedResourceIds(IReadOnlyDictionary<string, string> idMap)
        {
            return idMap?.Any(item => !string.Equals(item.Key, item.Value, StringComparison.OrdinalIgnoreCase)) == true;
        }

        private static bool IsTextResourcePath(string path)
        {
            string extension = Path.GetExtension(path ?? string.Empty);
            return extension.Equals(".xml", StringComparison.OrdinalIgnoreCase) ||
                   extension.Equals(".trail", StringComparison.OrdinalIgnoreCase) ||
                   extension.Equals(".reanim", StringComparison.OrdinalIgnoreCase) ||
                   extension.Equals(".txt", StringComparison.OrdinalIgnoreCase) ||
                   extension.Equals(".lua", StringComparison.OrdinalIgnoreCase);
        }

        private static List<ReanimTween> CloneTweens(IEnumerable<ReanimTween> tweens)
        {
            return tweens?
                .Select(tween => new ReanimTween
                {
                    TrackIndex = tween.TrackIndex,
                    TrackName = tween.TrackName,
                    StartFrame = tween.StartFrame,
                    EndFrame = tween.EndFrame,
                    AnchorX = tween.AnchorX,
                    AnchorY = tween.AnchorY,
                    Properties = tween.Properties?.ToList() ?? []
                })
                .ToList() ?? [];
        }

        private static bool TryFindImageAsset(ProjectManifest manifest, string assetId, out ImageAsset asset)
        {
            asset = manifest.Images.FirstOrDefault(item => string.Equals(item.Id, assetId, StringComparison.OrdinalIgnoreCase));
            return asset is not null;
        }

        private static bool TryFindAsset<TAsset>(IEnumerable<TAsset> assets, string assetId, out TAsset asset)
            where TAsset : EffectAsset
        {
            asset = assets.FirstOrDefault(item => string.Equals(item.Id, assetId, StringComparison.OrdinalIgnoreCase));
            return asset is not null;
        }

        private static string GetExistingAssetPath(ProjectManifest manifest, EffectAssetKind kind, string assetId)
        {
            return kind switch
            {
                EffectAssetKind.Image => manifest.Images.FirstOrDefault(asset => string.Equals(asset.Id, assetId, StringComparison.OrdinalIgnoreCase))?.Path ?? string.Empty,
                EffectAssetKind.Font => manifest.Fonts.FirstOrDefault(asset => string.Equals(asset.Id, assetId, StringComparison.OrdinalIgnoreCase))?.Path ?? string.Empty,
                EffectAssetKind.Reanim => manifest.Reanims.FirstOrDefault(asset => string.Equals(asset.Id, assetId, StringComparison.OrdinalIgnoreCase))?.Path ?? string.Empty,
                EffectAssetKind.Particle => manifest.Particles.FirstOrDefault(asset => string.Equals(asset.Id, assetId, StringComparison.OrdinalIgnoreCase))?.Path ?? string.Empty,
                EffectAssetKind.Trail => manifest.Trails.FirstOrDefault(asset => string.Equals(asset.Id, assetId, StringComparison.OrdinalIgnoreCase))?.Path ?? string.Empty,
                EffectAssetKind.Showcase => manifest.Showcases.FirstOrDefault(asset => string.Equals(asset.Id, assetId, StringComparison.OrdinalIgnoreCase))?.Path ?? string.Empty,
                _ => string.Empty
            };
        }

        private static void TryDeleteDirectory(string path)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, recursive: true);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
            }
        }

        private string CreateUniqueProjectDirectory(string sourceName, string existingDirectory = null)
        {
            string baseName = ProjectPathUtility.CreateSafeName(sourceName, "project");
            string projectsRoot = GetProjectsRootPath();

            string candidate = Path.Combine(projectsRoot, baseName);
            if (IsAvailableProjectDirectory(candidate, existingDirectory))
            {
                return candidate;
            }

            for (int i = 2; ; i++)
            {
                candidate = Path.Combine(projectsRoot, $"{baseName}-{i}");
                if (IsAvailableProjectDirectory(candidate, existingDirectory))
                {
                    return candidate;
                }
            }
        }

        private string GetProjectsRootPath()
        {
            string projectsRoot = _storageProvider.ProjectsRootPath;
            if (string.IsNullOrWhiteSpace(projectsRoot))
            {
                projectsRoot = new DefaultProjectStorageProvider().ProjectsRootPath;
            }

            Directory.CreateDirectory(projectsRoot);
            return Path.GetFullPath(projectsRoot);
        }

        private string EnsureInternalProjectDirectory(string projectDirectory)
        {
            if (string.IsNullOrWhiteSpace(projectDirectory))
            {
                throw new InvalidOperationException("No internal project is selected.");
            }

            string projectsRoot = TrimTrailingSeparators(GetProjectsRootPath());
            string fullPath = TrimTrailingSeparators(Path.GetFullPath(projectDirectory));
            string parentPath = TrimTrailingSeparators(Path.GetFullPath(Path.GetDirectoryName(fullPath) ?? string.Empty));
            if (!PathsEqual(parentPath, projectsRoot))
            {
                throw new InvalidOperationException("Only projects stored in the app private project folder can be managed.");
            }

            string manifestPath = Path.Combine(fullPath, ManifestFileName);
            if (!Directory.Exists(fullPath) || !File.Exists(manifestPath))
            {
                throw new InvalidOperationException("The selected project could not be found.");
            }

            return fullPath;
        }

        private static bool IsAvailableProjectDirectory(string candidate, string existingDirectory)
        {
            if (!string.IsNullOrWhiteSpace(existingDirectory) &&
                PathsEqual(candidate, existingDirectory))
            {
                return true;
            }

            return !Directory.Exists(candidate) &&
                !File.Exists(candidate) &&
                !File.Exists(Path.Combine(candidate, ManifestFileName));
        }

        private static string TrimTrailingSeparators(string path)
        {
            return path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        private static bool PathsEqual(string left, string right)
        {
            if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
            {
                return false;
            }

            return string.Equals(
                TrimTrailingSeparators(Path.GetFullPath(left)),
                TrimTrailingSeparators(Path.GetFullPath(right)),
                StringComparison.OrdinalIgnoreCase);
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
            if (extension is ".ttf")
            {
                return EffectAssetKind.Font;
            }

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
                EffectAssetKind.Font => [".ttf"],
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
                EffectAssetKind.Font => FontsDirectory,
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
                EffectAssetKind.Font => manifest.Fonts.Any(asset => comparer.Equals(asset.Id, assetId)),
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

                case EffectAssetKind.Font:
                    manifest.Fonts.Add(new FontAsset
                    {
                        Id = assetId,
                        Path = relativePath,
                        TrueType = string.Equals(Path.GetExtension(relativePath), ".ttf", StringComparison.OrdinalIgnoreCase),
                        FontSize = 32,
                        BorderSize = 0
                    });
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

        private static int FindManifestAssetIndex<T>(List<T> assets, string assetId, string projectPath)
            where T : EffectAsset
        {
            int index = assets.FindIndex(asset => AssetMatches(asset.Id, asset.Path, assetId, projectPath));
            if (index < 0)
            {
                throw new InvalidOperationException("The selected resource could not be found.");
            }

            return index;
        }

        private static bool AssetMatches(string id, string path, string assetId, string projectPath)
        {
            return (!string.IsNullOrWhiteSpace(assetId) &&
                    string.Equals(id, assetId, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrWhiteSpace(projectPath) &&
                    string.Equals(path, projectPath, StringComparison.OrdinalIgnoreCase));
        }

        private static void AddProjectPath(List<string> paths, string path)
        {
            if (!string.IsNullOrWhiteSpace(path))
            {
                paths.Add(path);
            }
        }

        private static void DeleteProjectFileIfExists(EffectProject project, string path)
        {
            string fullPath = ResolveProjectFilePath(project, path);
            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
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
                    ParticleDefinitionCodec.Encode(stream, ParticleDefinitionUtility.CreateEmpty(), targetFileName);
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
                scene.log("{safeId} initialized")
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
            manifest.Fonts ??= [];
            manifest.Reanims ??= [];
            manifest.Particles ??= [];
            manifest.Trails ??= [];
            manifest.Showcases ??= [];

            foreach (ImageAsset image in manifest.Images)
            {
                image.Rows = image.Rows < 1 ? 1 : image.Rows;
                image.Cols = image.Cols < 1 ? 1 : image.Cols;
            }

            foreach (FontAsset font in manifest.Fonts)
            {
                if (font.TrueType || string.Equals(Path.GetExtension(font.Path), ".ttf", StringComparison.OrdinalIgnoreCase))
                {
                    font.TrueType = true;
                    font.FontSize = font.FontSize <= 0 ? 32 : font.FontSize;
                    font.BorderSize = Math.Max(0, font.BorderSize);
                }
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
                    tween.AnchorX = float.IsFinite(tween.AnchorX) ? tween.AnchorX : 0.5f;
                    tween.AnchorY = float.IsFinite(tween.AnchorY) ? tween.AnchorY : 0.5f;
                    tween.Properties = ReanimTween.CreateTweenedProperties();
                }

                reanim.Tweens.RemoveAll(tween => tween.EndFrame <= tween.StartFrame);
            }
        }
    }
}
