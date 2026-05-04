using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using EffectViewer.Assets;
using EffectViewer.TodLib.Common;
using EffectViewer.TodLib.Particle;
using EffectViewer.TodLib.Reanim;
using EffectViewer.TodLib.Trail;

namespace EffectViewer.Projects
{
    public sealed class ResourceFolderImporter
    {
        private const string ImportOperation = "Importing Resource Folder";
        private const string ImagesDirectory = "assets/images";
        private const string FontsDirectory = "assets/fonts";
        private const string ReanimsDirectory = "assets/reanims";
        private const string ParticlesDirectory = "assets/particles";
        private const string TrailsDirectory = "assets/trails";

        private static readonly string[] ImageExtensions = [".png", ".jpg", ".jpeg", ".gif"];

        public async Task<FolderImportResult> ImportAsync(
            IResourceFolderSource source,
            string projectDirectory,
            IProgress<ProjectTransferProgress> progress = null)
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (string.IsNullOrWhiteSpace(projectDirectory))
            {
                throw new ArgumentException("Project directory is required when importing assets.", nameof(projectDirectory));
            }

            progress?.Report(new ProjectTransferProgress
            {
                Operation = ImportOperation,
                Message = "Scanning resource folder"
            });

            IReadOnlyList<ResourceFolderFile> files = await source.EnumerateFilesAsync(progress);
            Dictionary<string, ResourceFolderFile> fileIndex = files.ToDictionary(file => file.RelativePath, StringComparer.OrdinalIgnoreCase);
            int totalWorkItems = await CountImportWorkItemsAsync(source, files, fileIndex);
            FolderImportProgress importProgress = new(progress, totalWorkItems);

            Directory.CreateDirectory(projectDirectory);
            EnsureProjectDirectories(projectDirectory);
            importProgress.Advance("Prepared project folders");

            ProjectManifest manifest = new()
            {
                Name = string.IsNullOrWhiteSpace(source.Name) ? "Imported Resource Folder" : source.Name
            };

            Dictionary<string, ImageAsset> images = new(StringComparer.OrdinalIgnoreCase);
            List<FontAsset> fonts = [];
            HashSet<string> copiedProjectPaths = new(StringComparer.OrdinalIgnoreCase);
            HashSet<string> knownSourceFiles = new(StringComparer.OrdinalIgnoreCase);
            int missingImages = 0;

            string resourcesPath = "properties/resources.xml";
            if (fileIndex.ContainsKey(resourcesPath))
            {
                missingImages += await ImportResourcesXmlAsync(
                    source,
                    resourcesPath,
                    fileIndex,
                    projectDirectory,
                    images,
                    fonts,
                    copiedProjectPaths,
                    knownSourceFiles,
                    importProgress);
            }

            await AddImagesByConventionAsync(source, files, fileIndex, projectDirectory, images, copiedProjectPaths, knownSourceFiles, importProgress);
            if (await source.DirectoryExistsAsync("compiled"))
            {
                await AddCompiledReanimFilesAsync(source, fileIndex, projectDirectory, "compiled/reanim", ReanimsDirectory, manifest.Reanims, copiedProjectPaths, importProgress);
                await AddCompiledEffectFilesAsync(source, fileIndex, projectDirectory, "compiled/particles", ".xml.compiled", ".xml", ParticlesDirectory, manifest.Particles, copiedProjectPaths, importProgress);
                await AddCompiledEffectFilesAsync(source, fileIndex, projectDirectory, "compiled/particles", ".trail.compiled", ".trail", TrailsDirectory, manifest.Trails, copiedProjectPaths, importProgress);
                await AddCompiledEffectFilesAsync(source, fileIndex, projectDirectory, "compiled/trails", ".trail.compiled", ".trail", TrailsDirectory, manifest.Trails, copiedProjectPaths, importProgress);
            }
            else
            {
                await AddReanimFilesAsync(source, files, projectDirectory, "reanim", ".reanim", ReanimsDirectory, manifest.Reanims, copiedProjectPaths, importProgress);
                await AddEffectFilesAsync(source, files, projectDirectory, "particles", ".xml", ParticlesDirectory, manifest.Particles, copiedProjectPaths, importProgress);
                await AddEffectFilesAsync(source, files, projectDirectory, "particles", ".trail", TrailsDirectory, manifest.Trails, copiedProjectPaths, importProgress);
            }

            manifest.Images = images.Values
                .OrderBy(asset => asset.Id, StringComparer.OrdinalIgnoreCase)
                .ToList();
            manifest.Fonts = fonts
                .OrderBy(asset => asset.Id, StringComparer.OrdinalIgnoreCase)
                .ToList();
            importProgress.Advance("Built project manifest");

            EffectProject project = new(projectDirectory, manifest);
            return new FolderImportResult(
                project,
                manifest.Images.Count,
                manifest.Fonts.Count,
                manifest.Reanims.Count,
                manifest.Particles.Count,
                manifest.Trails.Count,
                missingImages);
        }

        private static void EnsureProjectDirectories(string projectDirectory)
        {
            Directory.CreateDirectory(Path.Combine(projectDirectory, ImagesDirectory.Replace('/', Path.DirectorySeparatorChar)));
            Directory.CreateDirectory(Path.Combine(projectDirectory, FontsDirectory.Replace('/', Path.DirectorySeparatorChar)));
            Directory.CreateDirectory(Path.Combine(projectDirectory, ReanimsDirectory.Replace('/', Path.DirectorySeparatorChar)));
            Directory.CreateDirectory(Path.Combine(projectDirectory, ParticlesDirectory.Replace('/', Path.DirectorySeparatorChar)));
            Directory.CreateDirectory(Path.Combine(projectDirectory, TrailsDirectory.Replace('/', Path.DirectorySeparatorChar)));
        }

        private static async Task<int> CountImportWorkItemsAsync(
            IResourceFolderSource source,
            IReadOnlyList<ResourceFolderFile> files,
            IReadOnlyDictionary<string, ResourceFolderFile> fileIndex)
        {
            int count = 2;

            if (fileIndex.ContainsKey("properties/resources.xml"))
            {
                count += await CountResourcesXmlAssetsAsync(source, "properties/resources.xml");
            }

            count += files.Count(file => IsImageFile(file.RelativePath));

            if (await source.DirectoryExistsAsync("compiled"))
            {
                count += CountFiles(files, "compiled/reanim", ".reanim.compiled");
                count += CountFiles(files, "compiled/particles", ".xml.compiled");
                count += CountFiles(files, "compiled/particles", ".trail.compiled");
                count += CountFiles(files, "compiled/trails", ".trail.compiled");
            }
            else
            {
                count += CountFiles(files, "reanim", ".reanim");
                count += CountFiles(files, "particles", ".xml");
                count += CountFiles(files, "particles", ".trail");
            }

            return count;
        }

        private static async Task<int> CountResourcesXmlAssetsAsync(IResourceFolderSource source, string resourcesPath)
        {
            int count = 0;
            Stream stream;
            try
            {
                stream = await source.OpenReadAsync(resourcesPath);
            }
            catch (Exception)
            {
                return 0;
            }

            using (stream)
            {
            SexyXmlParser parser = SexyXmlParser.FromStream(stream, resourcesPath);
            while (parser.TryNextElement(out SexyXmlElement element))
            {
                if (element.Type == SexyXmlElementType.Start && element.Value is "Image" or "Font")
                {
                    count++;
                }
            }
            }

            return count;
        }

        private static int CountFiles(IReadOnlyList<ResourceFolderFile> files, string relativeDirectory, string suffix)
        {
            string directory = ResourceFolderPath.Normalize(relativeDirectory);
            return files.Count(file => IsInDirectory(file.RelativePath, directory) &&
                                       file.RelativePath.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));
        }

        private static async Task<int> ImportResourcesXmlAsync(
            IResourceFolderSource source,
            string resourcesPath,
            IReadOnlyDictionary<string, ResourceFolderFile> fileIndex,
            string projectDirectory,
            Dictionary<string, ImageAsset> images,
            IList<FontAsset> fonts,
            HashSet<string> copiedProjectPaths,
            HashSet<string> knownSourceFiles,
            FolderImportProgress progress)
        {
            Stream stream = await TryOpenSourceFileAsync(source, resourcesPath, progress, advanceOnFailure: false);
            if (stream is null)
            {
                progress.Report($"Skipped {resourcesPath}");
                return 0;
            }

            using (stream)
            {
            SexyXmlParser parser = SexyXmlParser.FromStream(stream, resourcesPath);
            string currentPath = string.Empty;
            string currentPrefix = string.Empty;
            int missingImages = 0;

            while (parser.TryNextElement(out SexyXmlElement element))
            {
                if (element.Type != SexyXmlElementType.Start ||
                    element.Value is not ("SetDefaults" or "Image" or "Font"))
                {
                    continue;
                }

                if (element.Value == "SetDefaults")
                {
                    currentPath = ReadAttribute(element, "path") ?? currentPath;
                    currentPrefix = ReadAttribute(element, "idprefix") ?? currentPrefix;
                    continue;
                }

                if (element.Value == "Image")
                {
                    string rawId = ReadAttribute(element, "id") ?? string.Empty;
                    string rawPath = ReadAttribute(element, "path") ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(rawId) || string.IsNullOrWhiteSpace(rawPath))
                    {
                        progress.Advance("Skipped incomplete image resource");
                        continue;
                    }

                    string id = BuildResourceId(rawId, currentPrefix);

                    ResourceFolderFile sourceFile = FindImageFile(fileIndex, currentPath, rawPath, out bool alphaOnly);
                    if (sourceFile is null)
                    {
                        missingImages++;
                        progress.Advance($"Missing image {rawPath}");
                        continue;
                    }

                    progress.Report($"Importing image {sourceFile.RelativePath}");
                    string imagePath = await CopyAssetFileAsync(source, projectDirectory, sourceFile, ImagesDirectory, id, copiedProjectPaths, progress);
                    if (imagePath is null)
                    {
                        continue;
                    }

                    ImageAsset asset = new()
                    {
                        Id = id,
                        Path = imagePath,
                        AlphaOnly = alphaOnly,
                        Rows = ReadPositiveInt(element, "rows", 1),
                        Cols = ReadPositiveInt(element, "cols", 1)
                    };
                    knownSourceFiles.Add(sourceFile.RelativePath);
                    await AttachAlphaCompanionAsync(source, fileIndex, projectDirectory, asset, sourceFile, copiedProjectPaths, knownSourceFiles);

                    images[id] = asset;
                    progress.Advance($"Imported image {id}");
                    continue;
                }

                if (element.Value == "Font")
                {
                    string rawId = ReadAttribute(element, "id") ?? string.Empty;
                    string rawPath = ReadAttribute(element, "path") ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(rawId) || string.IsNullOrWhiteSpace(rawPath))
                    {
                        progress.Advance("Skipped incomplete font resource");
                        continue;
                    }

                    string id = BuildResourceId(rawId, currentPrefix);
                    if (ContainsAssetId(fonts, id))
                    {
                        progress.Advance($"Skipped duplicate font {id}");
                        continue;
                    }

                    ResourceFolderFile sourceFile = FindFontFile(fileIndex, currentPath, rawPath);
                    if (sourceFile is null)
                    {
                        progress.Advance($"Missing font {rawPath}");
                        continue;
                    }

                    progress.Report($"Importing font {sourceFile.RelativePath}");
                    string fontPath = await CopyAssetFileAsync(source, projectDirectory, sourceFile, FontsDirectory, id, copiedProjectPaths, progress);
                    if (fontPath is null)
                    {
                        continue;
                    }

                    fonts.Add(new FontAsset
                    {
                        Id = id,
                        Path = fontPath,
                        TrueType = IsTrueTypeFontPath(fontPath),
                        FontSize = IsTrueTypeFontPath(fontPath) ? 32 : 0
                    });
                    knownSourceFiles.Add(sourceFile.RelativePath);
                    progress.Advance($"Imported font {id}");
                }
            }

            return missingImages;
            }
        }

        private static async Task AddImagesByConventionAsync(
            IResourceFolderSource source,
            IReadOnlyList<ResourceFolderFile> files,
            IReadOnlyDictionary<string, ResourceFolderFile> fileIndex,
            string projectDirectory,
            Dictionary<string, ImageAsset> images,
            HashSet<string> copiedProjectPaths,
            HashSet<string> knownSourceFiles,
            FolderImportProgress progress)
        {
            foreach (ResourceFolderFile file in files.Where(file => IsImageFile(file.RelativePath)))
            {
                if (knownSourceFiles.Contains(file.RelativePath))
                {
                    progress.Advance($"Skipped known image {file.RelativePath}");
                    continue;
                }

                if (IsAlphaCompanionFile(file.RelativePath) && TryFindAlphaBaseFile(fileIndex, file, out _))
                {
                    progress.Advance($"Skipped alpha companion {file.RelativePath}");
                    continue;
                }

                string name = ResourceFolderPath.GetFileNameWithoutExtension(file.RelativePath);
                string id = EffectProjectService.CreateImageAssetId(name);

                if (images.ContainsKey(id))
                {
                    progress.Advance($"Skipped duplicate image {id}");
                    continue;
                }

                progress.Report($"Importing image {file.RelativePath}");
                string imagePath = await CopyAssetFileAsync(source, projectDirectory, file, ImagesDirectory, id, copiedProjectPaths, progress);
                if (imagePath is null)
                {
                    continue;
                }

                ImageAsset asset = new()
                {
                    Id = id,
                    Path = imagePath,
                    AlphaOnly = IsAlphaOnlyImageSource(fileIndex, file),
                    Rows = 1,
                    Cols = 1
                };
                knownSourceFiles.Add(file.RelativePath);
                await AttachAlphaCompanionAsync(source, fileIndex, projectDirectory, asset, file, copiedProjectPaths, knownSourceFiles);

                images[id] = asset;
                progress.Advance($"Imported image {id}");
            }
        }

        private static async Task AddEffectFilesAsync(
            IResourceFolderSource source,
            IReadOnlyList<ResourceFolderFile> files,
            string projectDirectory,
            string relativeDirectory,
            string extension,
            string assetDirectory,
            IList<EffectAsset> target,
            HashSet<string> copiedProjectPaths,
            FolderImportProgress progress)
        {
            string directory = ResourceFolderPath.Normalize(relativeDirectory);
            foreach (ResourceFolderFile file in files.Where(file => IsInDirectory(file.RelativePath, directory) &&
                                                                    file.RelativePath.EndsWith(extension, StringComparison.OrdinalIgnoreCase)))
            {
                string id = ResourceFolderPath.GetFileNameWithoutExtension(file.RelativePath);
                progress.Report($"Importing {id}");
                string assetPath = await CopyAssetFileAsync(source, projectDirectory, file, assetDirectory, id, copiedProjectPaths, progress);
                if (assetPath is null)
                {
                    continue;
                }

                target.Add(new EffectAsset
                {
                    Id = id,
                    Path = assetPath
                });
                progress.Advance($"Imported {id}");
            }
        }

        private static async Task AddCompiledEffectFilesAsync(
            IResourceFolderSource source,
            IReadOnlyDictionary<string, ResourceFolderFile> fileIndex,
            string projectDirectory,
            string relativeDirectory,
            string suffix,
            string sourceExtension,
            string assetDirectory,
            IList<EffectAsset> target,
            HashSet<string> copiedProjectPaths,
            FolderImportProgress progress)
        {
            string directory = ResourceFolderPath.Normalize(relativeDirectory);
            foreach (ResourceFolderFile file in fileIndex.Values.Where(file => IsInDirectory(file.RelativePath, directory) &&
                                                                               file.RelativePath.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)))
            {
                string name = ResourceFolderPath.GetFileName(file.RelativePath);
                string id = name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)
                    ? name[..^suffix.Length]
                    : ResourceFolderPath.GetFileNameWithoutExtension(file.RelativePath);
                if (ContainsAssetId(target, id))
                {
                    progress.Advance($"Skipped duplicate {id}");
                    continue;
                }

                progress.Report($"Converting {id}");
                string assetPath = await ConvertCompiledEffectFileAsync(source, projectDirectory, file, assetDirectory, id, sourceExtension, copiedProjectPaths, progress);
                if (assetPath is null)
                {
                    continue;
                }

                target.Add(new EffectAsset
                {
                    Id = id,
                    Path = assetPath
                });
                progress.Advance($"Converted {id}");
            }
        }

        private static async Task AddReanimFilesAsync(
            IResourceFolderSource source,
            IReadOnlyList<ResourceFolderFile> files,
            string projectDirectory,
            string relativeDirectory,
            string extension,
            string assetDirectory,
            IList<ReanimAsset> target,
            HashSet<string> copiedProjectPaths,
            FolderImportProgress progress)
        {
            string directory = ResourceFolderPath.Normalize(relativeDirectory);
            foreach (ResourceFolderFile file in files.Where(file => IsInDirectory(file.RelativePath, directory) &&
                                                                    file.RelativePath.EndsWith(extension, StringComparison.OrdinalIgnoreCase)))
            {
                string id = ResourceFolderPath.GetFileNameWithoutExtension(file.RelativePath);
                progress.Report($"Importing {id}");
                string assetPath = await CopyAssetFileAsync(source, projectDirectory, file, assetDirectory, id, copiedProjectPaths, progress);
                if (assetPath is null)
                {
                    continue;
                }

                target.Add(new ReanimAsset
                {
                    Id = id,
                    Path = assetPath
                });
                progress.Advance($"Imported {id}");
            }
        }

        private static async Task AddCompiledReanimFilesAsync(
            IResourceFolderSource source,
            IReadOnlyDictionary<string, ResourceFolderFile> fileIndex,
            string projectDirectory,
            string relativeDirectory,
            string assetDirectory,
            IList<ReanimAsset> target,
            HashSet<string> copiedProjectPaths,
            FolderImportProgress progress)
        {
            string directory = ResourceFolderPath.Normalize(relativeDirectory);
            const string suffix = ".reanim.compiled";
            foreach (ResourceFolderFile file in fileIndex.Values.Where(file => IsInDirectory(file.RelativePath, directory) &&
                                                                               file.RelativePath.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)))
            {
                string name = ResourceFolderPath.GetFileName(file.RelativePath);
                string id = name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)
                    ? name[..^suffix.Length]
                    : ResourceFolderPath.GetFileNameWithoutExtension(file.RelativePath);
                if (ContainsAssetId(target, id))
                {
                    progress.Advance($"Skipped duplicate {id}");
                    continue;
                }

                progress.Report($"Converting {id}");
                string assetPath = await ConvertCompiledReanimFileAsync(source, projectDirectory, file, assetDirectory, id, copiedProjectPaths, progress);
                if (assetPath is null)
                {
                    continue;
                }

                target.Add(new ReanimAsset
                {
                    Id = id,
                    Path = assetPath
                });
                progress.Advance($"Converted {id}");
            }
        }

        private static ResourceFolderFile FindImageFile(
            IReadOnlyDictionary<string, ResourceFolderFile> fileIndex,
            string relativeDirectory,
            string rawPath,
            out bool alphaOnly)
        {
            alphaOnly = false;
            string path = ResourceFolderPath.Combine(relativeDirectory, rawPath);
            if (ResourceFolderPath.HasExtension(path))
            {
                if (fileIndex.TryGetValue(path, out ResourceFolderFile file))
                {
                    return file;
                }

                return FindAlphaOnlyImageFile(fileIndex, path, out alphaOnly);
            }

            foreach (string extension in ImageExtensions)
            {
                string candidate = path + extension;
                if (fileIndex.TryGetValue(candidate, out ResourceFolderFile file))
                {
                    return file;
                }
            }

            return FindAlphaOnlyImageFile(fileIndex, path, out alphaOnly);
        }

        private static ResourceFolderFile FindFontFile(
            IReadOnlyDictionary<string, ResourceFolderFile> fileIndex,
            string relativeDirectory,
            string rawPath)
        {
            string path = ResourceFolderPath.Combine(relativeDirectory, rawPath);
            if (ResourceFolderPath.HasExtension(path))
            {
                return fileIndex.TryGetValue(path, out ResourceFolderFile file) ? file : null;
            }

            string candidate = path + ".txt";
            if (fileIndex.TryGetValue(candidate, out ResourceFolderFile fontFile))
            {
                return fontFile;
            }

            candidate = path + ".ttf";
            return fileIndex.TryGetValue(candidate, out fontFile) ? fontFile : null;
        }

        private static bool IsTrueTypeFontPath(string path)
        {
            return string.Equals(Path.GetExtension(path ?? string.Empty), ".ttf", StringComparison.OrdinalIgnoreCase);
        }

        private static string BuildResourceId(string rawId, string currentPrefix)
        {
            if (string.IsNullOrWhiteSpace(rawId))
            {
                return string.Empty;
            }

            if (HasKnownResourcePrefix(rawId) ||
                (!string.IsNullOrWhiteSpace(currentPrefix) && rawId.StartsWith(currentPrefix, StringComparison.OrdinalIgnoreCase)))
            {
                return rawId;
            }

            return (currentPrefix ?? string.Empty) + rawId;
        }

        private static bool HasKnownResourcePrefix(string id)
        {
            return id.StartsWith("IMAGE_", StringComparison.OrdinalIgnoreCase) ||
                   id.StartsWith("FONT_", StringComparison.OrdinalIgnoreCase) ||
                   id.StartsWith("SOUND_", StringComparison.OrdinalIgnoreCase);
        }

        private static async Task AttachAlphaCompanionAsync(
            IResourceFolderSource source,
            IReadOnlyDictionary<string, ResourceFolderFile> fileIndex,
            string projectDirectory,
            ImageAsset asset,
            ResourceFolderFile sourceFile,
            HashSet<string> copiedProjectPaths,
            HashSet<string> knownSourceFiles)
        {
            if (asset is null ||
                string.IsNullOrWhiteSpace(asset.Path) ||
                sourceFile is null)
            {
                return;
            }

            if (!TryFindAlphaCompanionFile(fileIndex, sourceFile, out ResourceFolderFile alphaFile))
            {
                return;
            }

            string alphaId = asset.Id + ".alpha";
            string alphaPath = await CopyAssetFileAsync(source, projectDirectory, alphaFile, ImagesDirectory, alphaId, copiedProjectPaths, progress: null);
            if (alphaPath is null)
            {
                return;
            }

            asset.AlphaPath = alphaPath;
            knownSourceFiles.Add(alphaFile.RelativePath);
        }

        private static bool TryFindAlphaCompanionFile(
            IReadOnlyDictionary<string, ResourceFolderFile> fileIndex,
            ResourceFolderFile sourceFile,
            out ResourceFolderFile alphaFile)
        {
            alphaFile = null;
            string directory = ResourceFolderPath.GetDirectoryName(sourceFile.RelativePath);
            string name = ResourceFolderPath.GetFileNameWithoutExtension(sourceFile.RelativePath);
            if (string.IsNullOrWhiteSpace(name) || name.EndsWith("_", StringComparison.Ordinal))
            {
                return false;
            }

            foreach (string extension in ImageExtensions)
            {
                string leadingCandidate = ResourceFolderPath.Combine(directory, "_" + name + extension);
                if (fileIndex.TryGetValue(leadingCandidate, out alphaFile))
                {
                    return true;
                }

                string candidate = ResourceFolderPath.Combine(directory, name + "_" + extension);
                if (fileIndex.TryGetValue(candidate, out alphaFile))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryFindAlphaBaseFile(
            IReadOnlyDictionary<string, ResourceFolderFile> fileIndex,
            ResourceFolderFile alphaFile,
            out ResourceFolderFile baseFile)
        {
            baseFile = null;
            string directory = ResourceFolderPath.GetDirectoryName(alphaFile.RelativePath);
            string name = ResourceFolderPath.GetFileNameWithoutExtension(alphaFile.RelativePath);
            if (string.IsNullOrWhiteSpace(name) ||
                (!name.EndsWith("_", StringComparison.Ordinal) && !name.StartsWith("_", StringComparison.Ordinal)))
            {
                return false;
            }

            string baseName = name.EndsWith("_", StringComparison.Ordinal)
                ? name[..^1]
                : name[1..];
            if (string.IsNullOrWhiteSpace(baseName))
            {
                return false;
            }

            foreach (string extension in ImageExtensions)
            {
                string candidate = ResourceFolderPath.Combine(directory, baseName + extension);
                if (fileIndex.TryGetValue(candidate, out baseFile))
                {
                    return true;
                }
            }

            return false;
        }

        private static ResourceFolderFile FindAlphaOnlyImageFile(
            IReadOnlyDictionary<string, ResourceFolderFile> fileIndex,
            string path,
            out bool alphaOnly)
        {
            alphaOnly = false;
            string directory = ResourceFolderPath.GetDirectoryName(path);
            string name = ResourceFolderPath.GetFileNameWithoutExtension(path);
            string extension = ResourceFolderPath.GetExtension(path);

            IEnumerable<string> extensions = string.IsNullOrEmpty(extension)
                ? ImageExtensions
                : [extension];

            foreach (string candidateExtension in extensions)
            {
                string leadingCandidate = ResourceFolderPath.Combine(directory, "_" + name + candidateExtension);
                if (fileIndex.TryGetValue(leadingCandidate, out ResourceFolderFile leadingFile))
                {
                    alphaOnly = true;
                    return leadingFile;
                }

                string trailingCandidate = ResourceFolderPath.Combine(directory, name + "_" + candidateExtension);
                if (fileIndex.TryGetValue(trailingCandidate, out ResourceFolderFile trailingFile))
                {
                    alphaOnly = true;
                    return trailingFile;
                }
            }

            return null;
        }

        private static bool IsAlphaOnlyImageSource(
            IReadOnlyDictionary<string, ResourceFolderFile> fileIndex,
            ResourceFolderFile file)
        {
            if (file is null)
            {
                return false;
            }

            string name = ResourceFolderPath.GetFileNameWithoutExtension(file.RelativePath);
            bool looksLikeAlphaFile = name.StartsWith("_", StringComparison.Ordinal) ||
                                      name.EndsWith("_", StringComparison.Ordinal);
            return looksLikeAlphaFile && !TryFindAlphaBaseFile(fileIndex, file, out _);
        }

        private static async Task<string> CopyAssetFileAsync(
            IResourceFolderSource source,
            string projectDirectory,
            ResourceFolderFile sourceFile,
            string assetDirectory,
            string preferredName,
            HashSet<string> copiedProjectPaths,
            FolderImportProgress progress)
        {
            string extension = ResourceFolderPath.GetExtension(sourceFile.RelativePath);
            string relativePath = CreateAssetRelativePath(assetDirectory, preferredName, extension, copiedProjectPaths);

            string destination = Path.Combine(projectDirectory, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            await using Stream input = await TryOpenSourceFileAsync(source, sourceFile.RelativePath, progress, advanceOnFailure: true);
            if (input is null)
            {
                return null;
            }

            await using FileStream output = File.Create(destination);
            await input.CopyToAsync(output);
            copiedProjectPaths.Add(relativePath);
            return relativePath;
        }

        private static async Task<string> ConvertCompiledEffectFileAsync(
            IResourceFolderSource source,
            string projectDirectory,
            ResourceFolderFile sourceFile,
            string assetDirectory,
            string preferredName,
            string sourceExtension,
            HashSet<string> copiedProjectPaths,
            FolderImportProgress progress)
        {
            string relativePath = CreateAssetRelativePath(assetDirectory, preferredName, sourceExtension, copiedProjectPaths);
            string destination = Path.Combine(projectDirectory, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);

            await using Stream input = await TryOpenSourceFileAsync(source, sourceFile.RelativePath, progress, advanceOnFailure: true);
            if (input is null)
            {
                return null;
            }

            await using FileStream output = File.Create(destination);
            if (string.Equals(sourceExtension, ".trail", StringComparison.OrdinalIgnoreCase))
            {
                TrailDefinition definition = TrailReader.Decode(input);
                TrailReader.WriteXml(output, definition);
            }
            else
            {
                TodParticleDefinition definition = SexyParticleReader.Decode(input);
                SexyParticleReader.WriteXml(output, definition);
            }

            copiedProjectPaths.Add(relativePath);
            return relativePath;
        }

        private static async Task<string> ConvertCompiledReanimFileAsync(
            IResourceFolderSource source,
            string projectDirectory,
            ResourceFolderFile sourceFile,
            string assetDirectory,
            string preferredName,
            HashSet<string> copiedProjectPaths,
            FolderImportProgress progress)
        {
            string relativePath = CreateAssetRelativePath(assetDirectory, preferredName, ".reanim", copiedProjectPaths);
            string destination = Path.Combine(projectDirectory, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);

            await using Stream input = await TryOpenSourceFileAsync(source, sourceFile.RelativePath, progress, advanceOnFailure: true);
            if (input is null)
            {
                return null;
            }

            await using FileStream output = File.Create(destination);
            ReanimatorDefinition definition = ReanimReader.Decode(input);
            ReanimReader.WriteXml(output, definition);

            copiedProjectPaths.Add(relativePath);
            return relativePath;
        }

        private static async Task<Stream> TryOpenSourceFileAsync(
            IResourceFolderSource source,
            string relativePath,
            FolderImportProgress progress,
            bool advanceOnFailure)
        {
            try
            {
                return await source.OpenReadAsync(relativePath);
            }
            catch (Exception ex)
            {
                if (advanceOnFailure)
                {
                    progress?.Advance($"Skipped {relativePath}: {ex.Message}");
                }
                else
                {
                    progress?.Report($"Skipped {relativePath}: {ex.Message}");
                }

                return null;
            }
        }

        private static string CreateAssetRelativePath(
            string assetDirectory,
            string preferredName,
            string extension,
            HashSet<string> copiedProjectPaths)
        {
            string safeName = ProjectPathUtility.CreateSafeName(preferredName, "asset");
            string relativePath = ProjectPathUtility.ToProjectRelativePath(Path.Combine(assetDirectory, safeName + extension));
            return EnsureUniquePath(relativePath, copiedProjectPaths);
        }

        private static bool IsInDirectory(string path, string directory)
        {
            string normalizedPath = ResourceFolderPath.Normalize(path);
            string normalizedDirectory = ResourceFolderPath.Normalize(directory);
            return string.Equals(ResourceFolderPath.GetDirectoryName(normalizedPath), normalizedDirectory, StringComparison.OrdinalIgnoreCase);
        }

        private static bool ContainsAssetId<TAsset>(IEnumerable<TAsset> assets, string id)
            where TAsset : EffectAsset
        {
            return assets.Any(asset => string.Equals(asset.Id, id, StringComparison.OrdinalIgnoreCase));
        }

        private static string EnsureUniquePath(string relativePath, HashSet<string> copiedProjectPaths)
        {
            if (!copiedProjectPaths.Contains(relativePath))
            {
                return relativePath;
            }

            string directory = Path.GetDirectoryName(relativePath)?.Replace('\\', '/') ?? string.Empty;
            string name = Path.GetFileNameWithoutExtension(relativePath);
            string extension = Path.GetExtension(relativePath);
            for (int i = 2; ; i++)
            {
                string candidateName = $"{name}-{i}{extension}";
                string candidate = string.IsNullOrWhiteSpace(directory)
                    ? candidateName
                    : $"{directory}/{candidateName}";
                if (!copiedProjectPaths.Contains(candidate))
                {
                    return candidate;
                }
            }
        }

        private static string ReadAttribute(SexyXmlElement element, string name)
        {
            return element.Attributes.TryGetValue(name, out string value) ? value : null;
        }

        private static int ReadPositiveInt(SexyXmlElement element, string name, int fallback)
        {
            string value = ReadAttribute(element, name);
            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed) && parsed > 0
                ? parsed
                : fallback;
        }

        private static bool IsImageFile(string path)
        {
            string extension = ResourceFolderPath.GetExtension(path);
            return ImageExtensions.Any(item => string.Equals(item, extension, StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsAlphaCompanionFile(string path)
        {
            string name = ResourceFolderPath.GetFileNameWithoutExtension(path);
            return name.StartsWith("_", StringComparison.Ordinal) ||
                   name.EndsWith("_", StringComparison.Ordinal);
        }

        private sealed class FolderImportProgress
        {
            private readonly IProgress<ProjectTransferProgress> _progress;
            private readonly int _totalItems;
            private int _completedItems;

            public FolderImportProgress(IProgress<ProjectTransferProgress> progress, int totalItems)
            {
                _progress = progress;
                _totalItems = Math.Max(1, totalItems);
            }

            public void Report(string message)
            {
                _progress?.Report(new ProjectTransferProgress
                {
                    Operation = ImportOperation,
                    Message = message,
                    CompletedItems = _completedItems,
                    TotalItems = _totalItems
                });
            }

            public void Advance(string message)
            {
                _completedItems = Math.Min(_completedItems + 1, _totalItems);
                _progress?.Report(new ProjectTransferProgress
                {
                    Operation = ImportOperation,
                    Message = message,
                    CompletedItems = _completedItems,
                    TotalItems = _totalItems
                });
            }
        }
    }
}
