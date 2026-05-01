using System;
using System.IO;
using EffectViewer.Projects;

namespace EffectViewer.Rendering.TextureUpload
{
    public sealed class ProjectTextureSource : ITextureSource
    {
        private readonly EffectProject _project;
        private readonly GeneratedTextureSource _fallback = new();

        public ProjectTextureSource(EffectProject project)
        {
            _project = project;
        }

        public bool TryLoad(RenderTextureRef texture, out TextureUploadData data)
        {
            if (_project is null ||
                string.IsNullOrWhiteSpace(texture.Id) ||
                !_project.Assets.TryGetImage(texture.Id, out Assets.ImageAsset asset))
            {
                return _fallback.TryLoad(texture, out data);
            }

            string fullPath = ProjectPathUtility.ResolvePath(_project, asset.Path);

            if (AvaloniaBitmapTextureLoader.TryLoadFromFile(fullPath, out data))
            {
                string alphaPath = ProjectPathUtility.ResolvePath(_project, asset.AlphaPath);
                if (AvaloniaBitmapTextureLoader.TryLoadFromFile(alphaPath, out TextureUploadData alphaData))
                {
                    ApplyAlphaCompanion(data, alphaData);
                }

                return true;
            }

            return _fallback.TryLoad(texture, out data);
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

            bool useAlphaChannel = HasNonOpaqueAlpha(alphaPixels);
            for (int y = 0; y < data.Height; y++)
            {
                int alphaY = y * alphaData.Height / data.Height;
                for (int x = 0; x < data.Width; x++)
                {
                    int alphaX = x * alphaData.Width / data.Width;
                    int source = (alphaY * alphaData.Width + alphaX) * 4;
                    int target = (y * data.Width + x) * 4 + 3;
                    pixels[target] = useAlphaChannel
                        ? alphaPixels[source + 3]
                        : ToLuminance(alphaPixels[source], alphaPixels[source + 1], alphaPixels[source + 2]);
                }
            }
        }

        private static bool HasNonOpaqueAlpha(byte[] rgbaPixels)
        {
            for (int i = 3; i < rgbaPixels.Length; i += 4)
            {
                if (rgbaPixels[i] != byte.MaxValue)
                {
                    return true;
                }
            }

            return false;
        }

        private static byte ToLuminance(byte red, byte green, byte blue)
        {
            return (byte)((red * 299 + green * 587 + blue * 114 + 500) / 1000);
        }
    }
}
