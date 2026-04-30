namespace EffectViewer.Assets
{
    public sealed class ImageAsset
    {
        public string Id { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public string AlphaPath { get; set; } = string.Empty;
        public int Rows { get; set; } = 1;
        public int Cols { get; set; } = 1;
        public string SourcePath { get; set; } = string.Empty;
        public string AlphaSourcePath { get; set; } = string.Empty;
    }
}
