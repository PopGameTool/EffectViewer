namespace EffectViewer.EffectRuntime.Reanim
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
            mTransX = ReanimatorUtility.DEFAULT_FIELD_PLACEHOLDER;
            mTransY = ReanimatorUtility.DEFAULT_FIELD_PLACEHOLDER;
            mSkewX = ReanimatorUtility.DEFAULT_FIELD_PLACEHOLDER;
            mSkewY = ReanimatorUtility.DEFAULT_FIELD_PLACEHOLDER;
            mScaleX = ReanimatorUtility.DEFAULT_FIELD_PLACEHOLDER;
            mScaleY = ReanimatorUtility.DEFAULT_FIELD_PLACEHOLDER;
            mFrame = ReanimatorUtility.DEFAULT_FIELD_PLACEHOLDER;
            mAlpha = ReanimatorUtility.DEFAULT_FIELD_PLACEHOLDER;
            mImage = null;
            mFont = null;
            mText = "";
        }
    }
}