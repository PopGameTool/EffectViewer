namespace EffectViewer.Projects
{
    public sealed class ProjectResourceResult
    {
        public EffectProject Project { get; }
        public EffectAssetKind Kind { get; }
        public string AssetId { get; }
        public string ProjectPath { get; }

        public ProjectResourceResult(EffectProject project, EffectAssetKind kind, string assetId, string projectPath)
        {
            Project = project;
            Kind = kind;
            AssetId = assetId ?? string.Empty;
            ProjectPath = projectPath ?? string.Empty;
        }
    }
}
