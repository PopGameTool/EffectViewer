using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using EffectViewer.Assets;
using EffectViewer.TodLib.Common;
using EffectViewer.TodLib.Particle;
using EffectViewer.TodLib.Reanim;
using EffectViewer.TodLib.Trail;

namespace EffectViewer.Projects
{
    public sealed class PopCapResourceFolderImporter
    {
        private const string ImagesDirectory = "assets/images";
        private const string ReanimsDirectory = "assets/reanims";
        private const string ParticlesDirectory = "assets/particles";
        private const string TrailsDirectory = "assets/trails";

        private static readonly string[] ImageExtensions = [".png", ".jpg", ".jpeg", ".gif"];

        public FolderImportResult Import(string sourceDirectory, string projectDirectory)
        {
            if (string.IsNullOrWhiteSpace(sourceDirectory) || !Directory.Exists(sourceDirectory))
            {
                throw new DirectoryNotFoundException(sourceDirectory);
            }

            if (string.IsNullOrWhiteSpace(projectDirectory))
            {
                throw new ArgumentException("Project directory is required when importing assets.", nameof(projectDirectory));
            }

            Directory.CreateDirectory(projectDirectory);
            EnsureProjectDirectories(projectDirectory);

            ProjectManifest manifest = new()
            {
                Name = Path.GetFileName(sourceDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
            };

            Dictionary<string, ImageAsset> images = new(StringComparer.OrdinalIgnoreCase);
            HashSet<string> copiedProjectPaths = new(StringComparer.OrdinalIgnoreCase);
            HashSet<string> knownSourceFiles = new(StringComparer.OrdinalIgnoreCase);
            int missingImages = 0;

            string resourcesPath = Path.Combine(sourceDirectory, "properties", "resources.xml");
            if (File.Exists(resourcesPath))
            {
                missingImages += ImportResourcesXml(sourceDirectory, projectDirectory, resourcesPath, images, copiedProjectPaths, knownSourceFiles);
            }

            AddImagesByConvention(sourceDirectory, projectDirectory, images, copiedProjectPaths, knownSourceFiles);
            if (Directory.Exists(Path.Combine(sourceDirectory, "compiled")))
            {
                AddCompiledReanimFiles(sourceDirectory, projectDirectory, "compiled/reanim", ReanimsDirectory, manifest.Reanims, copiedProjectPaths);
                AddCompiledEffectFiles(sourceDirectory, projectDirectory, "compiled/particles", ".xml.compiled", ".xml", ParticlesDirectory, manifest.Particles, copiedProjectPaths);
                AddCompiledEffectFiles(sourceDirectory, projectDirectory, "compiled/particles", ".trail.compiled", ".trail", TrailsDirectory, manifest.Trails, copiedProjectPaths);
                AddCompiledEffectFiles(sourceDirectory, projectDirectory, "compiled/trails", ".trail.compiled", ".trail", TrailsDirectory, manifest.Trails, copiedProjectPaths);
            }
            else
            {
                AddReanimFiles(sourceDirectory, projectDirectory, "reanim", ".reanim", ReanimsDirectory, manifest.Reanims, copiedProjectPaths);
                AddEffectFiles(sourceDirectory, projectDirectory, "particles", ".xml", ParticlesDirectory, manifest.Particles, copiedProjectPaths);
                AddEffectFiles(sourceDirectory, projectDirectory, "particles", ".trail", TrailsDirectory, manifest.Trails, copiedProjectPaths);
            }

            manifest.Images = images.Values
                .OrderBy(asset => asset.Id, StringComparer.OrdinalIgnoreCase)
                .ToList();

            EffectProject project = new(projectDirectory, manifest);
            return new FolderImportResult(
                project,
                manifest.Images.Count,
                manifest.Reanims.Count,
                manifest.Particles.Count,
                manifest.Trails.Count,
                missingImages);
        }

        private static void EnsureProjectDirectories(string projectDirectory)
        {
            Directory.CreateDirectory(Path.Combine(projectDirectory, ImagesDirectory.Replace('/', Path.DirectorySeparatorChar)));
            Directory.CreateDirectory(Path.Combine(projectDirectory, ReanimsDirectory.Replace('/', Path.DirectorySeparatorChar)));
            Directory.CreateDirectory(Path.Combine(projectDirectory, ParticlesDirectory.Replace('/', Path.DirectorySeparatorChar)));
            Directory.CreateDirectory(Path.Combine(projectDirectory, TrailsDirectory.Replace('/', Path.DirectorySeparatorChar)));
        }

        private static int ImportResourcesXml(
            string sourceDirectory,
            string projectDirectory,
            string resourcesPath,
            Dictionary<string, ImageAsset> images,
            HashSet<string> copiedProjectPaths,
            HashSet<string> knownSourceFiles)
        {
            SexyXmlParser parser = SexyXmlParser.FromFile(resourcesPath);
            string currentPath = string.Empty;
            string currentPrefix = string.Empty;
            int missingImages = 0;

            while (parser.TryNextElement(out SexyXmlElement element))
            {
                if (element.Type != SexyXmlElementType.Start ||
                    element.Value is not ("SetDefaults" or "Image"))
                {
                    continue;
                }

                if (element.Value == "SetDefaults")
                {
                    currentPath = ReadAttribute(element, "path") ?? currentPath;
                    currentPrefix = ReadAttribute(element, "idprefix") ?? currentPrefix;
                    continue;
                }

                string rawId = ReadAttribute(element, "id") ?? string.Empty;
                string rawPath = ReadAttribute(element, "path") ?? string.Empty;
                if (string.IsNullOrWhiteSpace(rawId) || string.IsNullOrWhiteSpace(rawPath))
                {
                    continue;
                }

                string id = rawId.StartsWith(currentPrefix, StringComparison.OrdinalIgnoreCase)
                    ? rawId
                    : currentPrefix + rawId;

                string sourceFile = FindImageFile(sourceDirectory, currentPath, rawPath);
                if (sourceFile is null)
                {
                    missingImages++;
                    continue;
                }

                ImageAsset asset = new()
                {
                    Id = id,
                    Path = CopyAssetFile(projectDirectory, sourceFile, ImagesDirectory, id, copiedProjectPaths),
                    Rows = ReadPositiveInt(element, "rows", 1),
                    Cols = ReadPositiveInt(element, "cols", 1)
                };
                knownSourceFiles.Add(Path.GetFullPath(sourceFile));
                AttachAlphaCompanion(projectDirectory, asset, sourceFile, copiedProjectPaths, knownSourceFiles);

                images[id] = asset;
            }

            return missingImages;
        }

        private static void AddImagesByConvention(
            string sourceDirectory,
            string projectDirectory,
            Dictionary<string, ImageAsset> images,
            HashSet<string> copiedProjectPaths,
            HashSet<string> knownSourceFiles)
        {
            foreach (string file in Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories)
                         .Where(IsImageFile))
            {
                string fullPath = Path.GetFullPath(file);
                if (knownSourceFiles.Contains(fullPath))
                {
                    continue;
                }

                if (IsAlphaCompanionFile(file) && TryFindAlphaBaseFile(file, out _))
                {
                    continue;
                }

                string relativePath = Path.GetRelativePath(sourceDirectory, file);
                string topDirectory = GetTopDirectory(relativePath);
                string name = Path.GetFileNameWithoutExtension(file);
                string id = string.Equals(topDirectory, "reanim", StringComparison.OrdinalIgnoreCase)
                    ? "IMAGE_REANIM_" + name.ToUpperInvariant()
                    : "IMAGE_" + name.ToUpperInvariant();

                if (images.ContainsKey(id))
                {
                    continue;
                }

                ImageAsset asset = new()
                {
                    Id = id,
                    Path = CopyAssetFile(projectDirectory, file, ImagesDirectory, id, copiedProjectPaths),
                    Rows = 1,
                    Cols = 1
                };
                knownSourceFiles.Add(fullPath);
                AttachAlphaCompanion(projectDirectory, asset, file, copiedProjectPaths, knownSourceFiles);

                images[id] = asset;
            }
        }

        private static string GetTopDirectory(string relativePath)
        {
            int separator = relativePath.IndexOfAny([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar]);
            return separator < 0 ? string.Empty : relativePath[..separator];
        }

        private static void AddEffectFiles(
            string sourceDirectory,
            string projectDirectory,
            string relativeDirectory,
            string extension,
            string assetDirectory,
            IList<EffectAsset> target,
            HashSet<string> copiedProjectPaths)
        {
            string directory = Path.Combine(sourceDirectory, relativeDirectory);
            if (!Directory.Exists(directory))
            {
                return;
            }

            foreach (string file in Directory.EnumerateFiles(directory, "*" + extension, SearchOption.TopDirectoryOnly))
            {
                string id = Path.GetFileNameWithoutExtension(file);
                target.Add(new EffectAsset
                {
                    Id = id,
                    Path = CopyAssetFile(projectDirectory, file, assetDirectory, id, copiedProjectPaths)
                });
            }
        }

        private static void AddCompiledEffectFiles(
            string sourceDirectory,
            string projectDirectory,
            string relativeDirectory,
            string suffix,
            string sourceExtension,
            string assetDirectory,
            IList<EffectAsset> target,
            HashSet<string> copiedProjectPaths)
        {
            string directory = Path.Combine(sourceDirectory, NormalizeRelativeDirectory(relativeDirectory));
            if (!Directory.Exists(directory))
            {
                return;
            }

            foreach (string file in Directory.EnumerateFiles(directory, "*" + suffix, SearchOption.TopDirectoryOnly))
            {
                string id = Path.GetFileName(file);
                id = id.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)
                    ? id[..^suffix.Length]
                    : Path.GetFileNameWithoutExtension(file);
                if (ContainsAssetId(target, id))
                {
                    continue;
                }

                target.Add(new EffectAsset
                {
                    Id = id,
                    Path = ConvertCompiledEffectFile(projectDirectory, file, assetDirectory, id, sourceExtension, copiedProjectPaths)
                });
            }
        }

        private static void AddReanimFiles(
            string sourceDirectory,
            string projectDirectory,
            string relativeDirectory,
            string extension,
            string assetDirectory,
            IList<ReanimAsset> target,
            HashSet<string> copiedProjectPaths)
        {
            string directory = Path.Combine(sourceDirectory, relativeDirectory);
            if (!Directory.Exists(directory))
            {
                return;
            }

            foreach (string file in Directory.EnumerateFiles(directory, "*" + extension, SearchOption.TopDirectoryOnly))
            {
                string id = Path.GetFileNameWithoutExtension(file);
                target.Add(new ReanimAsset
                {
                    Id = id,
                    Path = CopyAssetFile(projectDirectory, file, assetDirectory, id, copiedProjectPaths)
                });
            }
        }

        private static void AddCompiledReanimFiles(
            string sourceDirectory,
            string projectDirectory,
            string relativeDirectory,
            string assetDirectory,
            IList<ReanimAsset> target,
            HashSet<string> copiedProjectPaths)
        {
            string directory = Path.Combine(sourceDirectory, NormalizeRelativeDirectory(relativeDirectory));
            if (!Directory.Exists(directory))
            {
                return;
            }

            const string suffix = ".reanim.compiled";
            foreach (string file in Directory.EnumerateFiles(directory, "*" + suffix, SearchOption.TopDirectoryOnly))
            {
                string id = Path.GetFileName(file);
                id = id.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)
                    ? id[..^suffix.Length]
                    : Path.GetFileNameWithoutExtension(file);
                if (ContainsAssetId(target, id))
                {
                    continue;
                }

                target.Add(new ReanimAsset
                {
                    Id = id,
                    Path = ConvertCompiledReanimFile(projectDirectory, file, assetDirectory, id, copiedProjectPaths)
                });
            }
        }

        private static string FindImageFile(string sourceDirectory, string relativeDirectory, string rawPath)
        {
            string path = rawPath.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
            string directory = Path.Combine(sourceDirectory, relativeDirectory ?? string.Empty);

            if (Path.HasExtension(path))
            {
                string fullPath = Path.Combine(directory, path);
                return File.Exists(fullPath) ? fullPath : null;
            }

            foreach (string extension in ImageExtensions)
            {
                string fullPath = Path.Combine(directory, path + extension);
                if (File.Exists(fullPath))
                {
                    return fullPath;
                }
            }

            return null;
        }

        private static void AttachAlphaCompanion(
            string projectDirectory,
            ImageAsset asset,
            string sourceFile,
            HashSet<string> copiedProjectPaths,
            HashSet<string> knownSourceFiles)
        {
            if (asset is null ||
                string.IsNullOrWhiteSpace(asset.Path) ||
                string.IsNullOrWhiteSpace(sourceFile))
            {
                return;
            }

            if (!TryFindAlphaCompanionFile(sourceFile, out string alphaFile))
            {
                return;
            }

            string alphaId = asset.Id + ".alpha";
            asset.AlphaPath = CopyAssetFile(projectDirectory, alphaFile, ImagesDirectory, alphaId, copiedProjectPaths);
            knownSourceFiles.Add(Path.GetFullPath(alphaFile));
        }

        private static bool TryFindAlphaCompanionFile(string sourceFile, out string alphaFile)
        {
            alphaFile = null;
            string directory = Path.GetDirectoryName(sourceFile) ?? string.Empty;
            string name = Path.GetFileNameWithoutExtension(sourceFile);
            if (string.IsNullOrWhiteSpace(directory) || name.EndsWith("_", StringComparison.Ordinal))
            {
                return false;
            }

            foreach (string extension in ImageExtensions)
            {
                string candidate = Path.Combine(directory, name + "_" + extension);
                if (File.Exists(candidate))
                {
                    alphaFile = candidate;
                    return true;
                }
            }

            return false;
        }

        private static bool TryFindAlphaBaseFile(string alphaFile, out string baseFile)
        {
            baseFile = null;
            string directory = Path.GetDirectoryName(alphaFile) ?? string.Empty;
            string name = Path.GetFileNameWithoutExtension(alphaFile);
            if (string.IsNullOrWhiteSpace(directory) || !name.EndsWith("_", StringComparison.Ordinal))
            {
                return false;
            }

            string baseName = name[..^1];
            if (string.IsNullOrWhiteSpace(baseName))
            {
                return false;
            }

            foreach (string extension in ImageExtensions)
            {
                string candidate = Path.Combine(directory, baseName + extension);
                if (File.Exists(candidate))
                {
                    baseFile = candidate;
                    return true;
                }
            }

            return false;
        }

        private static string CopyAssetFile(
            string projectDirectory,
            string sourceFile,
            string assetDirectory,
            string preferredName,
            HashSet<string> copiedProjectPaths)
        {
            string extension = Path.GetExtension(sourceFile);
            string relativePath = CreateAssetRelativePath(assetDirectory, preferredName, extension, copiedProjectPaths);

            string destination = Path.Combine(projectDirectory, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(sourceFile, destination, overwrite: true);
            copiedProjectPaths.Add(relativePath);
            return relativePath;
        }

        private static string ConvertCompiledEffectFile(
            string projectDirectory,
            string sourceFile,
            string assetDirectory,
            string preferredName,
            string sourceExtension,
            HashSet<string> copiedProjectPaths)
        {
            string relativePath = CreateAssetRelativePath(assetDirectory, preferredName, sourceExtension, copiedProjectPaths);
            string destination = Path.Combine(projectDirectory, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);

            using FileStream source = File.OpenRead(sourceFile);
            using FileStream target = File.Create(destination);
            if (string.Equals(sourceExtension, ".trail", StringComparison.OrdinalIgnoreCase))
            {
                TrailDefinition definition = TrailReader.Decode(source);
                TrailReader.WriteXml(target, definition);
            }
            else
            {
                TodParticleDefinition definition = SexyParticleReader.Decode(source);
                SexyParticleReader.WriteXml(target, definition);
            }

            copiedProjectPaths.Add(relativePath);
            return relativePath;
        }

        private static string ConvertCompiledReanimFile(
            string projectDirectory,
            string sourceFile,
            string assetDirectory,
            string preferredName,
            HashSet<string> copiedProjectPaths)
        {
            string relativePath = CreateAssetRelativePath(assetDirectory, preferredName, ".reanim", copiedProjectPaths);
            string destination = Path.Combine(projectDirectory, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);

            using FileStream source = File.OpenRead(sourceFile);
            using FileStream target = File.Create(destination);
            ReanimatorDefinition definition = ReanimReader.Decode(source);
            ReanimReader.WriteXml(target, definition);

            copiedProjectPaths.Add(relativePath);
            return relativePath;
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

        private static string NormalizeRelativeDirectory(string relativeDirectory)
        {
            return relativeDirectory.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
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
            string extension = Path.GetExtension(path);
            return ImageExtensions.Any(item => string.Equals(item, extension, StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsAlphaCompanionFile(string path)
        {
            return Path.GetFileNameWithoutExtension(path).EndsWith("_", StringComparison.Ordinal);
        }
    }
}
