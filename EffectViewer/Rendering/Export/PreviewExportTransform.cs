namespace EffectViewer.Rendering.Export
{
    public readonly record struct PreviewExportTransform(float Scale, float OffsetX, float OffsetY)
    {
        public static PreviewExportTransform Identity { get; } = new(1f, 0f, 0f);
    }
}

