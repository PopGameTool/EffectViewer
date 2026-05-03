using System;

namespace EffectViewer.Rendering.Export
{
    public sealed class PreviewExportOptions
    {
        private const double MinimumDurationSeconds = 1d / 240d;
        public const double MaximumTimeSeconds = 200d;

        public PreviewExportFormat Format { get; set; } = PreviewExportFormat.Png;
        public double CanvasScale { get; set; } = 1d;
        public int ReferenceWidth { get; set; } = 800;
        public int ReferenceHeight { get; set; } = 600;
        public int Fps { get; set; } = 30;
        public double DurationSeconds { get; set; } = 2d;
        public bool UseFrameRange { get; set; }
        public int StartFrameIndex { get; set; }
        public int EndFrameIndex { get; set; }
        public double SourceFps { get; set; } = 30d;
        public bool UseTimeRange { get; set; }
        public double StartTimeSeconds { get; set; }
        public double EndTimeSeconds { get; set; } = 2d;
        public bool ExportUntilComplete { get; set; }

        public bool IsAnimation => Format is PreviewExportFormat.PngSequenceZip or PreviewExportFormat.Gif or PreviewExportFormat.Webp;
        public bool StopOnProviderCompletion => IsAnimation && UseTimeRange && ExportUntilComplete;
        public double StartSeconds => UseTimeRange
            ? Math.Clamp(StartTimeSeconds, 0d, MaximumTimeSeconds)
            : UseFrameRange
            ? Math.Max(0, StartFrameIndex) / Math.Clamp(SourceFps, 1d, 240d)
            : 0d;
        public double EndSeconds => UseTimeRange
            ? ExportUntilComplete
                ? MaximumTimeSeconds
                : Math.Max(StartSeconds, Math.Clamp(EndTimeSeconds, 0d, MaximumTimeSeconds))
            : UseFrameRange
            ? Math.Max(StartFrameIndex, EndFrameIndex) / Math.Clamp(SourceFps, 1d, 240d)
            : StartSeconds + Math.Clamp(DurationSeconds, MinimumDurationSeconds, 600d);

        public int FrameCount => IsAnimation
            ? UseFrameRange || UseTimeRange
                ? Math.Max(1, (int)Math.Ceiling(Math.Max(0d, EndSeconds - StartSeconds) * Math.Clamp(Fps, 1, 240)) + 1)
                : Math.Max(1, (int)Math.Ceiling(Math.Clamp(DurationSeconds, MinimumDurationSeconds, 600d) * Math.Clamp(Fps, 1, 240)))
            : 1;

        public PreviewExportOptions Normalized()
        {
            int startFrame = Math.Max(0, StartFrameIndex);
            int endFrame = Math.Max(startFrame, EndFrameIndex);
            double startSeconds = Math.Clamp(StartTimeSeconds, 0d, MaximumTimeSeconds);
            double endSeconds = Math.Max(startSeconds, Math.Clamp(EndTimeSeconds, 0d, MaximumTimeSeconds));
            return new PreviewExportOptions
            {
                Format = Format,
                CanvasScale = Math.Clamp(CanvasScale, 0.1d, 16d),
                ReferenceWidth = Math.Clamp(ReferenceWidth, 16, 4096),
                ReferenceHeight = Math.Clamp(ReferenceHeight, 16, 4096),
                Fps = Math.Clamp(Fps, 1, 240),
                DurationSeconds = Math.Clamp(DurationSeconds, MinimumDurationSeconds, 600d),
                UseFrameRange = UseFrameRange,
                StartFrameIndex = startFrame,
                EndFrameIndex = endFrame,
                SourceFps = Math.Clamp(SourceFps, 1d, 240d),
                UseTimeRange = UseTimeRange,
                StartTimeSeconds = startSeconds,
                EndTimeSeconds = endSeconds,
                ExportUntilComplete = ExportUntilComplete
            };
        }
    }
}
