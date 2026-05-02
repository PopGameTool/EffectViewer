namespace EffectViewer.Runtime.Showcase
{
    public sealed class ShowcaseReanimationFrameRange
    {
        internal ShowcaseReanimationFrameRange(int start, int count)
        {
            start_frame = start;
            frame_count = count;
            end_frame = count <= 0 ? start : start + count - 1;
        }

        public int start_frame { get; }
        public int frame_count { get; }
        public int end_frame { get; }
    }
}
