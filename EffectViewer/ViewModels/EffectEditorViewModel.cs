using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using EffectViewer.Projects;
using EffectViewer.Rendering;
using EffectViewer.TodLib.Common;
using EffectViewer.TodLib.Trail;

namespace EffectViewer.ViewModels
{
    public sealed partial class EffectEditorViewModel : EditorViewModelBase
    {
        private readonly EffectProject _project;
        private readonly ReanimPreviewSimulation _reanimPreview;
        private const float TrailDefaultWidthOverLength = 1f;
        private const float TrailDefaultAlphaOverLength = 1f;
        private TrailDefinition _trailDefinition;
        private string _savedTrailImageId;
        private int _savedTrailMaxPoints;
        private float _savedTrailMinPointDistance;
        private bool _savedTrailLoops;
        private FloatParameterTrack _savedTrailWidthOverLength;
        private FloatParameterTrack _savedTrailAlphaOverLength;
        private string _selectedReanimLayer;
        private string _trailImageId;
        private int _trailMaxPoints;
        private double _trailMinPointDistance;
        private bool _trailLoops;
        private string _trailDefinitionError;
        private bool _suppressTrailPropertyChanges;

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
        public bool HasTrailControls => Kind == EffectAssetKind.Trail && _trailDefinition is not null;
        public string TrailImageId
        {
            get => _trailImageId;
            set
            {
                if (SetProperty(ref _trailImageId, value))
                {
                    ApplyTrailPropertyChanges();
                }
            }
        }

        public int TrailMaxPoints
        {
            get => _trailMaxPoints;
            set
            {
                int clamped = System.Math.Clamp(value, 2, TodLibConstants.MAX_TRAIL_POINTS);
                if (SetProperty(ref _trailMaxPoints, clamped))
                {
                    ApplyTrailPropertyChanges();
                }
            }
        }

        public double TrailMinPointDistance
        {
            get => _trailMinPointDistance;
            set
            {
                double clamped = System.Math.Max(0d, value);
                if (SetProperty(ref _trailMinPointDistance, clamped))
                {
                    ApplyTrailPropertyChanges();
                }
            }
        }

        public bool TrailLoops
        {
            get => _trailLoops;
            set
            {
                if (SetProperty(ref _trailLoops, value))
                {
                    ApplyTrailPropertyChanges();
                }
            }
        }

        public FloatParameterTrackViewModel TrailWidthOverLength { get; } = new("WidthOverLength", TrailDefaultWidthOverLength);
        public FloatParameterTrackViewModel TrailAlphaOverLength { get; } = new("AlphaOverLength", TrailDefaultAlphaOverLength);

        public string TrailDefinitionError
        {
            get => _trailDefinitionError;
            private set => SetProperty(ref _trailDefinitionError, value);
        }

        public bool HasTrailDefinitionError => !string.IsNullOrWhiteSpace(TrailDefinitionError);

        public override bool SupportsSave => Kind == EffectAssetKind.Trail;
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
            _project = project;
            AssetId = assetId;
            Path = path;
            FileSummary = new EffectFileAnalyzer().Analyze(project, kind, path);
            TrailWidthOverLength.Changed += OnTrailTrackChanged;
            TrailAlphaOverLength.Changed += OnTrailTrackChanged;
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
                InitializeTrailEditor();
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

        private void OnTrailTrackChanged(FloatParameterTrackViewModel track)
        {
            ApplyTrailPropertyChanges();
        }

        public override async Task SaveAsync(EffectProjectService projectService, EffectProject project)
        {
            if (Kind != EffectAssetKind.Trail || _trailDefinition is null)
            {
                await base.SaveAsync(projectService, project);
                return;
            }

            string fullPath = TrailPreviewFrameBuilder.ResolvePath(project, Path);
            if (string.IsNullOrWhiteSpace(fullPath))
            {
                return;
            }

            string directory = System.IO.Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            if (!TryApplyTrailTracks())
            {
                throw new InvalidDataException(TrailDefinitionError);
            }

            await using FileStream stream = File.Create(fullPath);
            TrailReader.Encode(stream, _trailDefinition);
            AcceptSavedState();
        }

        public override void AcceptSavedState()
        {
            if (Kind == EffectAssetKind.Trail && _trailDefinition is not null)
            {
                _savedTrailImageId = TrailImageId;
                _savedTrailMaxPoints = TrailMaxPoints;
                _savedTrailMinPointDistance = (float)TrailMinPointDistance;
                _savedTrailLoops = TrailLoops;
                _savedTrailWidthOverLength = CloneTrack(_trailDefinition.mWidthOverLength);
                _savedTrailAlphaOverLength = CloneTrack(_trailDefinition.mAlphaOverLength);
            }

            base.AcceptSavedState();
        }

        public override void DiscardChanges()
        {
            if (Kind == EffectAssetKind.Trail && _trailDefinition is not null)
            {
                SetTrailProperties(
                    _savedTrailImageId,
                    _savedTrailMaxPoints,
                    _savedTrailMinPointDistance,
                    _savedTrailLoops,
                    _savedTrailWidthOverLength,
                    _savedTrailAlphaOverLength,
                    markDirty: false);
            }

            base.DiscardChanges();
        }

        private void InitializeTrailEditor()
        {
            string fullPath = TrailPreviewFrameBuilder.ResolvePath(_project, Path);
            _trailDefinition = !string.IsNullOrWhiteSpace(fullPath) && File.Exists(fullPath)
                ? TrailPreviewFrameBuilder.LoadDefinition(fullPath)
                : new TrailDefinition();
            _trailDefinition.ApplyDefaults();

            SetTrailProperties(
                _trailDefinition.mImage ?? string.Empty,
                _trailDefinition.mMaxPoints,
                _trailDefinition.mMinPointDistance,
                TodCommon.TestBit((uint)_trailDefinition.mTrailFlags, (int)TrailFlags.Loops),
                _trailDefinition.mWidthOverLength,
                _trailDefinition.mAlphaOverLength,
                markDirty: false);
            AcceptSavedState();
            RefreshTrailPreview();
            OnPropertyChanged(nameof(HasTrailControls));
        }

        private void SetTrailProperties(
            string imageId,
            int maxPoints,
            float minPointDistance,
            bool loops,
            FloatParameterTrack widthOverLength,
            FloatParameterTrack alphaOverLength,
            bool markDirty)
        {
            _suppressTrailPropertyChanges = true;
            TrailImageId = imageId ?? string.Empty;
            TrailMaxPoints = maxPoints;
            TrailMinPointDistance = minPointDistance;
            TrailLoops = loops;
            TrailWidthOverLength.LoadFrom(widthOverLength);
            TrailAlphaOverLength.LoadFrom(alphaOverLength);
            _suppressTrailPropertyChanges = false;
            ApplyTrailPropertyChanges(markDirty);
        }

        private void ApplyTrailPropertyChanges(bool markDirty = true)
        {
            if (_trailDefinition is null || _suppressTrailPropertyChanges)
            {
                return;
            }

            _trailDefinition.mImage = string.IsNullOrWhiteSpace(TrailImageId) ? null : TrailImageId.Trim();
            _trailDefinition.mMaxPoints = System.Math.Clamp(TrailMaxPoints, 2, TodLibConstants.MAX_TRAIL_POINTS);
            _trailDefinition.mMinPointDistance = (float)System.Math.Max(0d, TrailMinPointDistance);
            SetTrailFlag(TrailFlags.Loops, TrailLoops);

            if (!TryApplyTrailTracks())
            {
                if (markDirty)
                {
                    MarkDirty();
                }

                return;
            }

            RefreshTrailPreview();

            if (markDirty)
            {
                MarkDirty();
            }
        }

        private void RefreshTrailPreview()
        {
            if (Kind != EffectAssetKind.Trail || _trailDefinition is null)
            {
                return;
            }

            if (PreviewFrameProvider is System.IDisposable disposableProvider)
            {
                disposableProvider.Dispose();
            }

            PreviewFrameProvider = new TrailPreviewSimulation(_trailDefinition, AssetId);
            PreviewFrame = TrailPreviewFrameBuilder.Build(_trailDefinition, AssetId);
        }

        private bool TryApplyTrailTracks()
        {
            try
            {
                TrailWidthOverLength.ApplyTo(_trailDefinition.mWidthOverLength);
                TrailAlphaOverLength.ApplyTo(_trailDefinition.mAlphaOverLength);
            }
            catch (System.Exception ex) when (ex is System.FormatException or System.OverflowException)
            {
                TrailDefinitionError = ex.Message;
                OnPropertyChanged(nameof(HasTrailDefinitionError));
                return false;
            }

            TrailDefinitionError = string.Empty;
            OnPropertyChanged(nameof(HasTrailDefinitionError));
            return true;
        }

        private void SetTrailFlag(TrailFlags flag, bool enabled)
        {
            int mask = 1 << (int)flag;
            if (enabled)
            {
                _trailDefinition.mTrailFlags |= mask;
            }
            else
            {
                _trailDefinition.mTrailFlags &= ~mask;
            }
        }

        private static FloatParameterTrack CloneTrack(FloatParameterTrack source)
        {
            FloatParameterTrack clone = new();
            if (source?.mNodes is null || source.mCountNodes <= 0)
            {
                clone.mNodes = [];
                clone.mCountNodes = 0;
                return clone;
            }

            int count = System.Math.Min(source.mCountNodes, source.mNodes.Length);
            clone.mNodes = new FloatParameterTrackNode[count];
            clone.mCountNodes = count;
            for (int i = 0; i < count; i++)
            {
                FloatParameterTrackNode node = source.mNodes[i];
                clone.mNodes[i] = new FloatParameterTrackNode
                {
                    mTime = node.mTime,
                    mLowValue = node.mLowValue,
                    mHighValue = node.mHighValue,
                    mCurveType = node.mCurveType,
                    mDistribution = node.mDistribution
                };
            }

            return clone;
        }

        public override void Dispose()
        {
            TrailWidthOverLength.Changed -= OnTrailTrackChanged;
            TrailAlphaOverLength.Changed -= OnTrailTrackChanged;
            base.Dispose();
        }
    }
}
