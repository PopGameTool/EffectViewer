using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using EffectViewer.Assets;

namespace EffectViewer.Projects
{
    public sealed class PopCapResourceFolderImporter
    {
        private static readonly string[] ImageExtensions = [".png", ".jpg", ".jpeg", ".gif"];
        public FolderImportResult Import(string sourceDirectory, string projectDirectory, ImportMode mode)
        {
            if (string.IsNullOrWhiteSpace(sourceDirectory) || !Directory.Exists(sourceDirectory))
            {
                throw new DirectoryNotFoundException(sourceDirectory);
            }

            if (mode == ImportMode.CopyIntoProject && string.IsNullOrWhiteSpace(projectDirectory))
            {
                throw new ArgumentException("Project directory is required when copying imported assets.", nameof(projectDirectory));
            }

            ProjectManifest manifest = new()
            {
                Name = Path.GetFileName(sourceDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
            };

            Dictionary<string, ImageAsset> images = new(StringComparer.OrdinalIgnoreCase);
            int missingImages = 0;

            string resourcesPath = Path.Combine(sourceDirectory, "properties", "resources.xml");
            if (File.Exists(resourcesPath))
            {
                missingImages += ImportResourcesXml(sourceDirectory, projectDirectory, mode, resourcesPath, images);
            }

            AddImagesByConvention(sourceDirectory, projectDirectory, mode, images);
            AddEffectFiles(sourceDirectory, projectDirectory, mode, "reanim", ".reanim", manifest.Reanims);
            AddEffectFiles(sourceDirectory, projectDirectory, mode, "particles", ".xml", manifest.Particles);
            AddEffectFiles(sourceDirectory, projectDirectory, mode, "particles", ".trail", manifest.Trails);
            AddEffectFiles(sourceDirectory, projectDirectory, mode, "trails", ".trail", manifest.Trails);

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

        private static int ImportResourcesXml(
            string sourceDirectory,
            string projectDirectory,
            ImportMode mode,
            string resourcesPath,
            Dictionary<string, ImageAsset> images)
        {
            XDocument document = XDocument.Load(resourcesPath, LoadOptions.PreserveWhitespace);
            string currentPath = string.Empty;
            string currentPrefix = string.Empty;
            int missingImages = 0;

            foreach (XElement element in document.Descendants().Where(element =>
                         element.Name.LocalName is "SetDefaults" or "Image"))
            {
                if (element.Name.LocalName == "SetDefaults")
                {
                    currentPath = (string)element.Attribute("path") ?? currentPath;
                    currentPrefix = (string)element.Attribute("idprefix") ?? currentPrefix;
                    continue;
                }

                string rawId = (string)element.Attribute("id") ?? string.Empty;
                string rawPath = (string)element.Attribute("path") ?? string.Empty;
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
                    Path = PrepareAssetPath(sourceDirectory, projectDirectory, mode, sourceFile, currentPath),
                    SourcePath = sourceFile,
                    Rows = ReadPositiveInt(element, "rows", 1),
                    Cols = ReadPositiveInt(element, "cols", 1)
                };
                AttachAlphaCompanion(sourceDirectory, projectDirectory, mode, asset);

                images[id] = asset;
            }

            return missingImages;
        }

        private static void AddImagesByConvention(
            string sourceDirectory,
            string projectDirectory,
            ImportMode mode,
            Dictionary<string, ImageAsset> images)
        {
            HashSet<string> knownSourceFiles = images.Values
                .Where(asset => !string.IsNullOrWhiteSpace(asset.SourcePath))
                .Select(asset => Path.GetFullPath(asset.SourcePath))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (ImageAsset asset in images.Values.Where(asset => !string.IsNullOrWhiteSpace(asset.AlphaSourcePath)))
            {
                knownSourceFiles.Add(Path.GetFullPath(asset.AlphaSourcePath));
            }

            foreach (string file in Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories)
                         .Where(IsImageFile))
            {
                string fullPath = Path.GetFullPath(file);
                if (knownSourceFiles.Contains(fullPath))
                {
                    continue;
                }

                string relativePath = Path.GetRelativePath(sourceDirectory, file);
                if (IsAlphaCompanionFile(file) && TryFindAlphaBaseFile(file, out _))
                {
                    continue;
                }

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
                    Path = PrepareAssetPath(sourceDirectory, projectDirectory, mode, file, topDirectory),
                    SourcePath = file,
                    Rows = 1,
                    Cols = 1
                };
                AttachAlphaCompanion(sourceDirectory, projectDirectory, mode, asset);
                if (!string.IsNullOrWhiteSpace(asset.AlphaSourcePath))
                {
                    knownSourceFiles.Add(Path.GetFullPath(asset.AlphaSourcePath));
                }

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
            ImportMode mode,
            string relativeDirectory,
            string extension,
            IList<EffectAsset> target)
        {
            string directory = Path.Combine(sourceDirectory, relativeDirectory);
            if (!Directory.Exists(directory))
            {
                return;
            }

            foreach (string file in Directory.EnumerateFiles(directory, "*" + extension, SearchOption.TopDirectoryOnly))
            {
                target.Add(new EffectAsset
                {
                    Id = Path.GetFileNameWithoutExtension(file),
                    Path = PrepareAssetPath(sourceDirectory, projectDirectory, mode, file, relativeDirectory),
                    SourcePath = file
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
            string sourceDirectory,
            string projectDirectory,
            ImportMode mode,
            ImageAsset asset)
        {
            if (asset is null ||
                string.IsNullOrWhiteSpace(asset.SourcePath) ||
                !TryFindAlphaCompanionFile(asset.SourcePath, out string alphaFile))
            {
                return;
            }

            asset.AlphaPath = PrepareAssetPath(sourceDirectory, projectDirectory, mode, alphaFile, GetTopDirectory(Path.GetRelativePath(sourceDirectory, alphaFile)));
            asset.AlphaSourcePath = alphaFile;
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

        private static string PrepareAssetPath(
            string sourceDirectory,
            string projectDirectory,
            ImportMode mode,
            string sourceFile,
            string preferredRelativeDirectory)
        {
            if (mode == ImportMode.ReferenceSource)
            {
                return Path.GetRelativePath(sourceDirectory, sourceFile).Replace('\\', '/');
            }

            string relativePath = Path.GetRelativePath(sourceDirectory, sourceFile);
            string destination = Path.Combine(projectDirectory, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(sourceFile, destination, overwrite: true);
            return relativePath.Replace('\\', '/');
        }

        private static int ReadPositiveInt(XElement element, string name, int fallback)
        {
            string value = (string)element.Attribute(name);
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
