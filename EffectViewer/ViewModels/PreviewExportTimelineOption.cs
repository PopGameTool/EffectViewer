using System;

namespace EffectViewer.ViewModels
{
    public sealed class PreviewExportTimelineOption
    {
        public const string FullTimelineId = "full";

        public PreviewExportTimelineOption(
            string id,
            string displayName,
            bool isFullTimeline,
            int frameStart,
            int frameCount,
            double sourceFps)
        {
            Id = string.IsNullOrWhiteSpace(id) ? FullTimelineId : id;
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? Id : displayName;
            IsFullTimeline = isFullTimeline;
            FrameStart = Math.Max(0, frameStart);
            FrameCount = Math.Max(0, frameCount);
            SourceFps = Math.Clamp(sourceFps, 0d, 240d);
        }

        public string Id { get; }
        public string DisplayName { get; }
        public bool IsFullTimeline { get; }
        public int FrameStart { get; }
        public int FrameCount { get; }
        public int FrameEnd => FrameCount <= 0 ? FrameStart : FrameStart + FrameCount - 1;
        public int StartFrameNumber => FrameStart + 1;
        public int EndFrameNumber => FrameCount <= 0 ? StartFrameNumber : FrameStart + FrameCount;
        public double SourceFps { get; }
        public bool HasKnownDuration => FrameCount > 0 && SourceFps > 0d;
        public double DurationSeconds => HasKnownDuration ? FrameCount / SourceFps : 0d;

        public override string ToString()
        {
            return DisplayName;
        }
    }
}
