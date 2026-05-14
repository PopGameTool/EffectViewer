using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using EffectViewer.Projects;
using EffectViewer.EffectRuntime.Graphics;

namespace EffectViewer.Rendering.TextureUpload
{
    public sealed class ProjectTextureSource : ITextureSource, ITextureRevisionSource
    {
        private readonly EffectProject _project;
        private readonly GeneratedTextureSource _fallback = new();
        private HashSet<string> _fontTextureIds;

        public ProjectTextureSource(EffectProject project)
        {
            _project = project;
        }

        public bool TryLoad(RenderTextureRef texture, out TextureUploadData data)
        {
            if (TrueTypeFontTextureRegistry.TryGet(texture.Id, out TrueTypeFontTextureSnapshot snapshot))
            {
                data = new TextureUploadData(snapshot.Width, snapshot.Height, snapshot.RgbaPixels, snapshot.Revision);
                return true;
            }

            if (_project is null ||
                string.IsNullOrWhiteSpace(texture.Id) ||
                !_project.Assets.TryGetImage(texture.Id, out Assets.ImageAsset asset))
            {
                return _fallback.TryLoad(texture, out data);
            }

            string fullPath = ProjectPathUtility.ResolvePath(_project, asset.Path);

            if (AvaloniaBitmapTextureLoader.TryLoadFromFile(fullPath, out data))
            {
                if (IsAlphaOnlyTexture(texture.Id, asset) ||
                    IsImplicitFontAlphaOnlyTexture(texture.Id, asset, data))
                {
                    ApplyAlphaOnlyMask(data);
                }
                else
                {
                    string alphaPath = ProjectPathUtility.ResolvePath(_project, asset.AlphaPath);
                    if (AvaloniaBitmapTextureLoader.TryLoadFromFile(alphaPath, out TextureUploadData alphaData))
                    {
                        ApplyAlphaCompanion(data, alphaData);
                    }
                }

                return true;
            }

            return _fallback.TryLoad(texture, out data);
        }

        public int GetTextureRevision(RenderTextureRef texture)
        {
            return TrueTypeFontTextureRegistry.TryGet(texture.Id, out TrueTypeFontTextureSnapshot snapshot)
                ? snapshot.Revision
                : 0;
        }

        private static void ApplyAlphaCompanion(TextureUploadData data, TextureUploadData alphaData)
        {
            if (data is null ||
                alphaData is null ||
                data.Width <= 0 ||
                data.Height <= 0 ||
                alphaData.Width <= 0 ||
                alphaData.Height <= 0)
            {
                return;
            }

            byte[] pixels = data.RgbaPixels;
            byte[] alphaPixels = alphaData.RgbaPixels;
            if (pixels.Length < data.Width * data.Height * 4 ||
                alphaPixels.Length < alphaData.Width * alphaData.Height * 4)
            {
                return;
            }

            for (int y = 0; y < data.Height; y++)
            {
                int alphaY = y * alphaData.Height / data.Height;
                for (int x = 0; x < data.Width; x++)
                {
                    int alphaX = x * alphaData.Width / data.Width;
                    int source = (alphaY * alphaData.Width + alphaX) * 4;
                    int target = (y * data.Width + x) * 4 + 3;
                    // LawnProject's ImageLib reads alpha companion masks from the low byte.
                    pixels[target] = alphaPixels[source + 2];
                }
            }
        }

        private bool IsAlphaOnlyTexture(string textureId, Assets.ImageAsset asset)
        {
            if (asset.AlphaOnly)
            {
                return true;
            }

            string id = string.IsNullOrWhiteSpace(asset.Id) ? textureId : asset.Id;
            return TryGetImplicitAlphaOnlyBaseId(id, out string baseId) &&
                   !_project.Assets.Images.ContainsKey(baseId);
        }

        private bool IsImplicitFontAlphaOnlyTexture(string textureId, Assets.ImageAsset asset, TextureUploadData data)
        {
            if (!string.IsNullOrWhiteSpace(asset.AlphaPath) ||
                !IsFontTexture(textureId) ||
                !LooksLikeOpaqueGrayscaleMask(data))
            {
                return false;
            }

            return true;
        }

        private bool IsFontTexture(string textureId)
        {
            if (string.IsNullOrWhiteSpace(textureId))
            {
                return false;
            }

            _fontTextureIds ??= BuildFontTextureIds();
            return _fontTextureIds.Contains(textureId);
        }

        private HashSet<string> BuildFontTextureIds()
        {
            HashSet<string> ids = new(StringComparer.OrdinalIgnoreCase);
            if (_project is null)
            {
                return ids;
            }

            foreach (Assets.FontAsset font in _project.Manifest.Fonts)
            {
                string descriptorPath = ProjectPathUtility.ResolvePath(_project, font.Path);
                if (string.IsNullOrWhiteSpace(descriptorPath) || !File.Exists(descriptorPath))
                {
                    continue;
                }

                string text;
                try
                {
                    text = File.ReadAllText(descriptorPath);
                }
                catch (Exception)
                {
                    continue;
                }

                foreach (Match match in Regex.Matches(
                    text,
                    "^\\s*LayerSetImage\\s+\\S+\\s+(?:(['\"])(?<name>.*?)\\1|(?<name>[^\\s;]+))",
                    RegexOptions.Multiline))
                {
                    string imageName = match.Groups["name"].Value;
                    string baseName = Path.GetFileNameWithoutExtension(imageName);
                    foreach (string candidate in EnumerateFontImageCandidates(imageName, baseName))
                    {
                        if (_project.Assets.TryGetImage(candidate, out Assets.ImageAsset image))
                        {
                            ids.Add(image.Id);
                            break;
                        }
                    }
                }
            }

            return ids;
        }

        private static IEnumerable<string> EnumerateFontImageCandidates(string imageName, string baseName)
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

        private static bool TryGetImplicitAlphaOnlyBaseId(string id, out string baseId)
        {
            baseId = null;
            if (string.IsNullOrWhiteSpace(id))
            {
                return false;
            }

            const string leadingAlphaPrefix = "IMAGE__";
            if (id.StartsWith(leadingAlphaPrefix, StringComparison.OrdinalIgnoreCase) &&
                id.Length > leadingAlphaPrefix.Length)
            {
                baseId = "IMAGE_" + id[leadingAlphaPrefix.Length..];
                return true;
            }

            const string imagePrefix = "IMAGE_";
            if (id.StartsWith(imagePrefix, StringComparison.OrdinalIgnoreCase) &&
                id.Length > imagePrefix.Length + 1 &&
                id.EndsWith("_", StringComparison.Ordinal))
            {
                baseId = id[..^1];
                return true;
            }

            return false;
        }

        private static bool LooksLikeOpaqueGrayscaleMask(TextureUploadData data)
        {
            if (data is null ||
                data.Width <= 0 ||
                data.Height <= 0 ||
                data.RgbaPixels.Length < data.Width * data.Height * 4)
            {
                return false;
            }

            int pixelCount = data.Width * data.Height;
            int grayCount = 0;
            int opaqueCount = 0;
            int darkCount = 0;
            int lightCount = 0;
            byte[] pixels = data.RgbaPixels;
            for (int i = 0; i < pixelCount * 4; i += 4)
            {
                byte red = pixels[i + 0];
                byte green = pixels[i + 1];
                byte blue = pixels[i + 2];
                byte alpha = pixels[i + 3];

                if (Math.Abs(red - green) <= 2 && Math.Abs(green - blue) <= 2)
                {
                    grayCount++;
                }

                if (alpha == byte.MaxValue)
                {
                    opaqueCount++;
                }

                if (blue <= 3)
                {
                    darkCount++;
                }

                if (blue >= 252)
                {
                    lightCount++;
                }
            }

            return opaqueCount >= pixelCount * 995L / 1000L &&
                   grayCount >= pixelCount * 995L / 1000L &&
                   darkCount >= pixelCount / 5 &&
                   lightCount >= pixelCount / 1000;
        }

        private static void ApplyAlphaOnlyMask(TextureUploadData data)
        {
            if (data is null ||
                data.Width <= 0 ||
                data.Height <= 0 ||
                data.RgbaPixels.Length < data.Width * data.Height * 4)
            {
                return;
            }

            byte[] pixels = data.RgbaPixels;
            for (int i = 0; i < data.Width * data.Height * 4; i += 4)
            {
                // Alpha-only masks become solid white with the mask stored as alpha.
                byte alpha = pixels[i + 2];
                pixels[i + 0] = byte.MaxValue;
                pixels[i + 1] = byte.MaxValue;
                pixels[i + 2] = byte.MaxValue;
                pixels[i + 3] = alpha;
            }
        }
    }
}
