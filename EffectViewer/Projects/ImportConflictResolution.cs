using System.Threading.Tasks;

namespace EffectViewer.Projects
{
    public enum ImportConflictResolution
    {
        Skip,
        Overwrite,
        KeepBoth
    }

    public sealed class ImportAssetConflict
    {
        public EffectAssetKind Kind { get; init; }
        public string AssetId { get; init; } = string.Empty;
        public string ExistingProjectPath { get; init; } = string.Empty;
        public string IncomingProjectPath { get; init; } = string.Empty;
    }

    public delegate Task<ImportConflictResolution> ImportConflictResolver(ImportAssetConflict conflict);
}
