namespace EffectViewer.Runtime.Showcase
{
    public sealed class ShowcaseReanimationTransform
    {
        internal ShowcaseReanimationTransform(ReanimatorTransform transform)
        {
            x = transform.mTransX;
            y = transform.mTransY;
            skew_x = transform.mSkewX;
            skew_y = transform.mSkewY;
            scale_x = transform.mScaleX;
            scale_y = transform.mScaleY;
            frame = transform.mFrame;
            alpha = transform.mAlpha;
            image = transform.mImage;
            font = transform.mFont;
            text = transform.mText;
        }

        public double x { get; }
        public double y { get; }
        public double skew_x { get; }
        public double skew_y { get; }
        public double scale_x { get; }
        public double scale_y { get; }
        public double frame { get; }
        public double alpha { get; }
        public string image { get; }
        public string font { get; }
        public string text { get; }
        public bool has_image => !string.IsNullOrEmpty(image);
        public bool has_font => !string.IsNullOrEmpty(font);
        public bool has_text => !string.IsNullOrEmpty(text);
        public bool is_blank => frame < 0;

        public ShowcaseVector position()
        {
            return new ShowcaseVector(x, y);
        }
    }
}
