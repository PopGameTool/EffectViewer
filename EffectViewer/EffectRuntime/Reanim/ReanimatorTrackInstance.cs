namespace EffectViewer.EffectRuntime.Reanim
{
    public struct ReanimatorTrackInstance
    {
        public int mBlendCounter;
        public int mBlendTime;
        public ReanimatorTransform mBlendTransform;
        public float mShakeOverride;
        public float mShakeX;
        public float mShakeY;
        public AttachmentID mAttachmentID;
        public Image mImageOverride;
        public string mFontOverride;
        public int mRenderGroup;
        public EffectColor mTrackColor;
        public bool mIgnoreClipRect;
        public bool mTruncateDisappearingFrames;
        public bool mIgnoreColorOverride;
        public bool mIgnoreExtraAdditiveColor;
        public bool mIsAttacher;

        public ReanimatorTrackInstance()
        {
            Reset();
        }

        public void Reset()
        {
            mBlendCounter = 0;
            mBlendTime = 0;
            mShakeOverride = 0f;
            mShakeX = 0f;
            mShakeY = 0f;
            mAttachmentID = AttachmentID.Null;
            mRenderGroup = 0;
            mIgnoreClipRect = false;
            mImageOverride = null;
            mFontOverride = null;
            mTruncateDisappearingFrames = true;
            mTrackColor = EffectColor.White;
            mIgnoreColorOverride = false;
            mIgnoreExtraAdditiveColor = false;
            mBlendTransform = new ReanimatorTransform();
            mIsAttacher = false;
        }
    }
}
