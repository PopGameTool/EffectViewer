using System;
using System.IO;
using SkiaSharp;

namespace EffectViewer.Rendering.Export
{
    public static class PngImageWriter
    {
        public static void Write(Stream stream, int width, int height, byte[] rgbaPixels)
        {
            ArgumentNullException.ThrowIfNull(stream);
            ValidateImage(width, height, rgbaPixels);

            SKImageInfo info = new(width, height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
            using SKBitmap bitmap = new(info);
            IntPtr destination = bitmap.GetPixels();
            System.Runtime.InteropServices.Marshal.Copy(rgbaPixels, 0, destination, width * height * 4);

            using SKImage image = SKImage.FromBitmap(bitmap);
            using SKData data = image.Encode(SKEncodedImageFormat.Png, quality: 100)
                ?? throw new InvalidOperationException("SkiaSharp could not encode the PNG image.");
            data.SaveTo(stream);
        }

        private static void ValidateImage(int width, int height, byte[] rgbaPixels)
        {
            if (width <= 0 || height <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(width), "Image dimensions must be positive.");
            }

            if (rgbaPixels is null || rgbaPixels.Length < width * height * 4)
            {
                throw new ArgumentException("RGBA pixel data is incomplete.", nameof(rgbaPixels));
            }
        }
    }
}
