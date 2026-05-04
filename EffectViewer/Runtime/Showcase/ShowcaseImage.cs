namespace EffectViewer.Runtime.Showcase
{
    public sealed class ShowcaseImage
    {
        internal ShowcaseImage(Image image)
        {
            Image = image;
            id = image?.mId;
            width = image?.mWidth ?? 0;
            height = image?.mHeight ?? 0;
            cols = image?.mNumCols ?? 0;
            rows = image?.mNumRows ?? 0;
            cel_width = image?.GetCelWidth() ?? 0;
            cel_height = image?.GetCelHeight() ?? 0;
            has_platform_image = image?.mPlatformImage is not null;
        }

        internal Image Image { get; }
        public string id { get; }
        public int width { get; }
        public int height { get; }
        public int cols { get; }
        public int rows { get; }
        public int num_cols => cols;
        public int num_rows => rows;
        public readonly int cel_width;
        public readonly int cel_height;
        public bool has_platform_image { get; }

        public int get_cel_width()
        {
            return cel_width;
        }

        public int get_cel_height()
        {
            return cel_height;
        }
    }
}
