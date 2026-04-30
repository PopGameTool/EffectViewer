namespace EffectViewer.Projects
{
    public sealed class EffectProject
    {
        public string RootPath { get; }
        public ProjectManifest Manifest { get; }
        public AssetIndex Assets { get; }

        public EffectProject(string rootPath, ProjectManifest manifest)
        {
            RootPath = rootPath;
            Manifest = manifest;
            Assets = new AssetIndex(manifest);
        }
    }
}
