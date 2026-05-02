namespace EffectViewer.Runtime.Showcase
{
    public sealed class ShowcaseImage
    {
        internal ShowcaseImage(Image image)
        {
            id = image?.mId;
            width = image?.mWidth ?? 0;
            height = image?.mHeight ?? 0;
            cols = image?.mNumCols ?? 0;
            rows = image?.mNumRows ?? 0;
            cel_width = image?.GetCelWidth() ?? 0;
            cel_height = image?.GetCelHeight() ?? 0;
            has_platform_image = image?.mPlatformImage is not null;
        }

        public string id { get; }
        public int width { get; }
        public int height { get; }
        public int cols { get; }
        public int rows { get; }
        public int cel_width { get; }
        public int cel_height { get; }
        public bool has_platform_image { get; }
    }
}
