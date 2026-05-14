using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EffectViewer.Projects;
using EffectViewer.EffectRuntime.Common;
using EffectViewer.EffectRuntime.Graphics;

namespace EffectViewer.Runtime
{
    public sealed class ProjectResourceProvider : IResourceProvider
    {
        private readonly EffectProject _project;
        private readonly Dictionary<string, Image> _images = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Font> _fonts = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _fontCacheKeys = new(StringComparer.OrdinalIgnoreCase);

        public ProjectResourceProvider(EffectProject project)
        {
            _project = project;
        }

        public Image GetImage(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return null;
            }

            if (_images.TryGetValue(id, out Image cached))
            {
                return cached;
            }

            if (!_project.Assets.TryGetImage(id, out Assets.ImageAsset asset))
            {
                return null;
            }

            Image image = new()
            {
                mId = asset.Id,
                mNumRows = Math.Max(1, asset.Rows),
                mNumCols = Math.Max(1, asset.Cols)
            };

            string fullPath = ProjectPathUtility.ResolvePath(_project, asset.Path);

            if (ImageFileSizeReader.TryReadSize(fullPath, out int width, out int height))
            {
                image.mWidth = width;
                image.mHeight = height;
            }

            if (File.Exists(fullPath))
            {
                image.mPlatformImage = fullPath;
            }

            _images[id] = image;
            return image;
        }

        public Font GetFont(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return null;
            }

            if (_fonts.TryGetValue(id, out Font cached))
            {
                if (_project.Assets.TryGetFont(id, out Assets.FontAsset cachedAsset) &&
                    _fontCacheKeys.TryGetValue(id, out string cachedKey) &&
                    string.Equals(cachedKey, CreateFontCacheKey(cachedAsset), StringComparison.Ordinal))
                {
                    return cached;
                }

                RemoveCachedFontReferences(cached);
            }

            if (!_project.Assets.TryGetFont(id, out Assets.FontAsset asset))
            {
                return null;
            }

            string fullPath = ProjectPathUtility.ResolvePath(_project, asset.Path);
            Font font = IsTrueTypeFont(asset)
                ? Font.LoadTrueType(asset.Id, fullPath, asset.FontSize, asset.BorderSize)
                : ImageFontDescriptorReader.Load(asset.Id, fullPath, ResolveFontLayerImage);
            if (font is null)
            {
                return null;
            }

            _fonts[id] = font;
            _fontCacheKeys[id] = CreateFontCacheKey(asset);
            if (!string.Equals(id, asset.Id, StringComparison.OrdinalIgnoreCase))
            {
                _fonts[asset.Id] = font;
                _fontCacheKeys[asset.Id] = CreateFontCacheKey(asset);
            }

            return font;
        }

        private Image ResolveFontLayerImage(string imageName)
        {
            if (string.IsNullOrWhiteSpace(imageName))
            {
                return null;
            }

            string baseName = Path.GetFileNameWithoutExtension(imageName);
            foreach (string candidate in EnumerateImageCandidates(imageName, baseName))
            {
                Image image = GetImage(candidate);
                if (image != null)
                {
                    return image;
                }
            }

            return null;
        }

        private void RemoveCachedFontReferences(Font font)
        {
            if (font is null)
            {
                return;
            }

            List<string> keys = _fonts
                .Where(pair => ReferenceEquals(pair.Value, font))
                .Select(pair => pair.Key)
                .ToList();

            foreach (string key in keys)
            {
                _fonts.Remove(key);
                _fontCacheKeys.Remove(key);
            }

            font.Dispose();
        }

        private static bool IsTrueTypeFont(Assets.FontAsset asset)
        {
            return asset?.TrueType == true ||
                string.Equals(Path.GetExtension(asset?.Path ?? string.Empty), ".ttf", StringComparison.OrdinalIgnoreCase);
        }

        private static string CreateFontCacheKey(Assets.FontAsset asset)
        {
            if (asset is null)
            {
                return string.Empty;
            }

            return string.Join(
                "|",
                asset.Path ?? string.Empty,
                IsTrueTypeFont(asset).ToString(),
                asset.FontSize.ToString(System.Globalization.CultureInfo.InvariantCulture),
                asset.BorderSize.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        private static IEnumerable<string> EnumerateImageCandidates(string imageName, string baseName)
        {
            yield return imageName;
            yield return baseName;
            yield return EffectProjectService.CreateImageAssetId(imageName);
            yield return EffectProjectService.CreateImageAssetId(baseName);

            if (!baseName.StartsWith("_", StringComparison.Ordinal))
            {
                yield return EffectProjectService.CreateImageAssetId("_" + baseName);
            }
        }
    }
}
