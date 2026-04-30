namespace EffectViewer.Rendering.TextureUpload
{
    public sealed class TextureUploadData
    {
        public int Width { get; }
        public int Height { get; }
        public byte[] RgbaPixels { get; }

        public TextureUploadData(int width, int height, byte[] rgbaPixels)
        {
            Width = width;
            Height = height;
            RgbaPixels = rgbaPixels;
        }
    }
}
