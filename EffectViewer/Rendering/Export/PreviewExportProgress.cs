namespace EffectViewer.Rendering.Export
{
    public enum PreviewExportProgressStage
    {
        CapturingFrames,
        RasterizingFrames,
        WritingFrames,
        EncodingImage
    }

    public sealed class PreviewExportProgress
    {
        public PreviewExportProgressStage Stage { get; init; }
        public int CompletedItems { get; init; }
        public int TotalItems { get; init; }
    }
}
