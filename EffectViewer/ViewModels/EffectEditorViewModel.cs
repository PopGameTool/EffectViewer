using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.Input;
using EffectViewer.Projects;
using EffectViewer.Rendering;

namespace EffectViewer.ViewModels
{
    public sealed partial class EffectEditorViewModel : EditorViewModelBase
    {
        private readonly ReanimPreviewSimulation _reanimPreview;
        private string _selectedReanimLayer;

        public string AssetId { get; }
        public string Path { get; }
        public string EditorSummary { get; }
        public EffectFileSummary FileSummary { get; }
        public ObservableCollection<string> ReanimLayers { get; } = [];
        public ObservableCollection<ReanimTrackViewModel> ReanimTracks { get; } = [];
        public bool IsReanimEditor => Kind == EffectAssetKind.Reanim;
        public bool IsParticleEditor => Kind == EffectAssetKind.Particle;
        public bool IsTrailEditor => Kind == EffectAssetKind.Trail;
        public bool HasReanimControls => Kind == EffectAssetKind.Reanim && ReanimTracks.Count > 0;
        public string SelectedReanimLayer
        {
            get => _selectedReanimLayer;
            set
            {
                if (SetProperty(ref _selectedReanimLayer, value))
                {
                    _reanimPreview?.SetLayer(value == "Full timeline" ? null : value);
                }
            }
        }

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
                _reanimPreview = new ReanimPreviewSimulation(project, path);
                PreviewFrameProvider = _reanimPreview;
                PreviewFrame = EffectPreviewFrameBuilder.BuildPlaceholder(kind, assetId);
                InitializeReanimControls();
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

        [RelayCommand]
        private void ShowAllTracks()
        {
            foreach (ReanimTrackViewModel track in ReanimTracks)
            {
                track.IsVisible = true;
            }
        }

        [RelayCommand]
        private void HideAllTracks()
        {
            foreach (ReanimTrackViewModel track in ReanimTracks)
            {
                track.IsVisible = false;
            }
        }

        private void InitializeReanimControls()
        {
            if (_reanimPreview is null)
            {
                return;
            }

            ReanimLayers.Add("Full timeline");
            foreach (string layerName in _reanimPreview.LayerNames)
            {
                if (!ReanimLayers.Contains(layerName))
                {
                    ReanimLayers.Add(layerName);
                }
            }

            SelectedReanimLayer = ReanimLayers.FirstOrDefault();

            for (int i = 0; i < _reanimPreview.TrackNames.Count; i++)
            {
                ReanimTrackViewModel track = new(i, _reanimPreview.TrackNames[i]);
                track.VisibilityChanged += OnReanimTrackVisibilityChanged;
                ReanimTracks.Add(track);
            }
        }

        private void OnReanimTrackVisibilityChanged(ReanimTrackViewModel track)
        {
            _reanimPreview?.SetTrackVisible(track.Index, track.IsVisible);
        }
    }
}
