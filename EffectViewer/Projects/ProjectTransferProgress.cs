namespace EffectViewer.Projects
{
    public sealed class ProjectTransferProgress
    {
        public string Operation { get; init; } = string.Empty;
        public string Message { get; init; } = string.Empty;
        public int CompletedItems { get; init; }
        public int TotalItems { get; init; }

        public double Ratio => TotalItems <= 0
            ? 0d
            : System.Math.Clamp((double)CompletedItems / TotalItems, 0d, 1d);
    }
}
