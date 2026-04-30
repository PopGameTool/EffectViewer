namespace EffectViewer.Assets
{
    public sealed class ImageMetadata
    {
        public int Rows { get; set; } = 1;
        public int Cols { get; set; } = 1;

        public int CellCount => Rows * Cols;
    }
}
