namespace EffectViewer.Runtime.Showcase
{
    public sealed class ShowcaseFrameTime
    {
        internal ShowcaseFrameTime(ReanimatorFrameTime frameTime)
        {
            fraction = frameTime.mFraction;
            frame_before = frameTime.mAnimFrameBeforeInt;
            frame_after = frameTime.mAnimFrameAfterInt;
        }

        public double fraction { get; }
        public int frame_before { get; }
        public int frame_after { get; }
    }
}
