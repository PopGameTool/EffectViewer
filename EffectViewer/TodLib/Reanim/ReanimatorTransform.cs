namespace EffectViewer.TodLib.Reanim
{
    public struct ReanimatorTransform
    {
        public float mTransX;
        public float mTransY;
        public float mSkewX;
        public float mSkewY;
        public float mScaleX;
        public float mScaleY;
        public float mFrame;
        public float mAlpha;
        public string mImage;
        public string mFont;
        public string mText;

        public ReanimatorTransform()
        {
            Reset();
        }

        private void Reset()
        {
            mTransX = ReanimatorXnaHelpers.DEFAULT_FIELD_PLACEHOLDER;
            mTransY = ReanimatorXnaHelpers.DEFAULT_FIELD_PLACEHOLDER;
            mSkewX = ReanimatorXnaHelpers.DEFAULT_FIELD_PLACEHOLDER;
            mSkewY = ReanimatorXnaHelpers.DEFAULT_FIELD_PLACEHOLDER;
            mScaleX = ReanimatorXnaHelpers.DEFAULT_FIELD_PLACEHOLDER;
            mScaleY = ReanimatorXnaHelpers.DEFAULT_FIELD_PLACEHOLDER;
            mFrame = ReanimatorXnaHelpers.DEFAULT_FIELD_PLACEHOLDER;
            mAlpha = ReanimatorXnaHelpers.DEFAULT_FIELD_PLACEHOLDER;
            mImage = null;
            mFont = null;
            mText = "";
        }
    }
}