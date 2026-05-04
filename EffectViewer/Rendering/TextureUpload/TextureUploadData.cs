namespace EffectViewer.Rendering.TextureUpload
{
    public sealed class TextureUploadData
    {
        public int Width { get; }
        public int Height { get; }
        public byte[] RgbaPixels { get; }
        public int Revision { get; }

        public TextureUploadData(int width, int height, byte[] rgbaPixels, int revision = 0)
        {
            Width = width;
            Height = height;
            RgbaPixels = rgbaPixels;
            Revision = revision;
        }
    }
}
