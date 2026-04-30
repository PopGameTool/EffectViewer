namespace EffectViewer.Projects
{
    public sealed class FolderImportResult
    {
        public EffectProject Project { get; }
        public int ImageCount { get; }
        public int ReanimCount { get; }
        public int ParticleCount { get; }
        public int TrailCount { get; }
        public int MissingImageCount { get; }

        public FolderImportResult(
            EffectProject project,
            int imageCount,
            int reanimCount,
            int particleCount,
            int trailCount,
            int missingImageCount)
        {
            Project = project;
            ImageCount = imageCount;
            ReanimCount = reanimCount;
            ParticleCount = particleCount;
            TrailCount = trailCount;
            MissingImageCount = missingImageCount;
        }
    }
}
