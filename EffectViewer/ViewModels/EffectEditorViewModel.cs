using EffectViewer.Projects;
using EffectViewer.Rendering;

namespace EffectViewer.ViewModels
{
    public sealed class EffectEditorViewModel : EditorViewModelBase
    {
        public string AssetId { get; }
        public string Path { get; }
        public string EditorSummary { get; }
        public EffectFileSummary FileSummary { get; }
        public string ImageReferenceSummary => FileSummary.ImageIds.Count == 0
            ? "No image references found."
            : string.Join(", ", FileSummary.ImageIds);
        public string ResolvedImageReferenceSummary => FileSummary.ImageResolutions.Count == 0
            ? "No resolved image references."
            : string.Join(", ", FileSummary.ImageResolutions);
        public string MissingImageSummary => FileSummary.MissingImageIds.Count == 0
            ? "No missing image references."
            : string.Join(", ", FileSummary.MissingImageIds);

        public EffectEditorViewModel(EffectAssetKind kind, string assetId, string path, EffectProject project)
            : base(assetId, kind)
        {
            AssetId = assetId;
            Path = path;
            FileSummary = new EffectFileAnalyzer().Analyze(project, kind, path);
            EditorSummary = kind switch
            {
                EffectAssetKind.Reanim => "Reanim editor shell: tracks, frames, transforms, and image bindings will live here.",
                EffectAssetKind.Particle => "Particle editor shell: emitter list, parameter tracks, fields, and live preview are available here.",
                EffectAssetKind.Trail => "Trail editor shell: width, alpha, duration, point settings, and path preview will live here.",
                _ => "Effect editor shell."
            };
            if (kind == EffectAssetKind.Reanim)
            {
                PreviewFrameProvider = new ReanimPreviewSimulation(project, path);
                PreviewFrame = EffectPreviewFrameBuilder.BuildPlaceholder(kind, assetId);
            }
            else if (kind == EffectAssetKind.Particle)
            {
                PreviewFrameProvider = new ParticlePreviewSimulation(project, path, assetId);
                PreviewFrame = EffectPreviewFrameBuilder.BuildPlaceholder(kind, assetId);
            }
            else if (kind == EffectAssetKind.Trail)
            {
                PreviewFrameProvider = new TrailPreviewSimulation(project, path, assetId);
                PreviewFrame = TrailPreviewFrameBuilder.Build(project, path, assetId);
            }
            else
            {
                PreviewFrame = EffectPreviewFrameBuilder.BuildPlaceholder(kind, assetId);
            }
            TextureSource = new Rendering.TextureUpload.ProjectTextureSource(project);
        }
    }
}
