namespace EffectViewer.Projects
{
    public sealed class EffectProject
    {
        public string RootPath { get; }
        public ProjectManifest Manifest { get; }
        public AssetIndex Assets { get; private set; }
        public EffectProjectDefinitionCache Definitions { get; }

        public EffectProject(string rootPath, ProjectManifest manifest)
        {
            RootPath = rootPath;
            Manifest = manifest;
            Assets = new AssetIndex(manifest);
            Definitions = new EffectProjectDefinitionCache(this);
        }

        public void RebuildAssetIndex()
        {
            Assets = new AssetIndex(Manifest);
        }
    }
}
