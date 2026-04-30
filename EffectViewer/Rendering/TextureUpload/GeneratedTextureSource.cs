namespace EffectViewer.Rendering.TextureUpload
{
    public sealed class GeneratedTextureSource : ITextureSource
    {
        public bool TryLoad(RenderTextureRef texture, out TextureUploadData data)
        {
            if (texture.Id == FrameCaptureGraphics.WhiteTextureId)
            {
                data = new TextureUploadData(1, 1, [255, 255, 255, 255]);
                return true;
            }

            const int width = 64;
            const int height = 64;
            byte[] pixels = new byte[width * height * 4];

            int seed = texture.Id?.GetHashCode() ?? 0;
            byte r = (byte)(80 + (seed & 0x7F));
            byte g = (byte)(80 + ((seed >> 8) & 0x7F));
            byte b = (byte)(80 + ((seed >> 16) & 0x7F));

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int offset = (y * width + x) * 4;
                    bool light = ((x / 8) + (y / 8)) % 2 == 0;
                    pixels[offset + 0] = light ? r : (byte)(r / 2);
                    pixels[offset + 1] = light ? g : (byte)(g / 2);
                    pixels[offset + 2] = light ? b : (byte)(b / 2);
                    pixels[offset + 3] = 255;
                }
            }

            data = new TextureUploadData(width, height, pixels);
            return true;
        }
    }
}
