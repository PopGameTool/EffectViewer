using System;
using System.IO;
using Avalonia.Media.Imaging;

namespace EffectViewer.Rendering.TextureUpload
{
    public static class AvaloniaBitmapTextureLoader
    {
        public static bool TryLoadFromFile(string path, out TextureUploadData data)
        {
            data = null;

            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return false;
            }

            try
            {
                using Bitmap bitmap = new(path);
                int width = bitmap.PixelSize.Width;
                int height = bitmap.PixelSize.Height;
                if (width <= 0 || height <= 0)
                {
                    return false;
                }

                byte[] pixels = new byte[width * height * 4];
                using MemoryLockedFramebuffer framebuffer = new(pixels, width, height);
                bitmap.CopyPixels(framebuffer);
                data = new TextureUploadData(width, height, pixels);
                return true;
            }
            catch (Exception)
            {
                data = null;
                return false;
            }
        }
    }
}
