using System.Collections.ObjectModel;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EffectViewer.Assets;
using EffectViewer.Projects;
using EffectViewer.Rendering;
using EffectViewer.TodLib.Common;
using EffectViewer.TodLib.Particle;
using EffectViewer.TodLib.Reanim;
using EffectViewer.TodLib.Trail;

namespace EffectViewer.ViewModels
{
    public sealed partial class EffectEditorViewModel : EditorViewModelBase, IViewportDragHandler
    {
        private readonly EffectProject _project;
        private readonly ReanimPreviewSimulation _reanimPreview;
        private readonly ReanimAsset _reanimAsset;
        private ReanimatorDefinition _reanimDefinition;
        private ReanimatorDefinition _savedReanimDefinition;
        private List<ReanimTween> _savedReanimTweens = [];
        private const float TrailDefaultWidthOverLength = 1f;
        private const float TrailDefaultAlphaOverLength = 1f;
        private const float TrailDefaultWidthOverTime = 1f;
        private const float TrailDefaultAlphaOverTime = 1f;
        private const float TrailDefaultDuration = 100f;
        private TrailDefinition _trailDefinition;
        private string _savedTrailImageId;
        private int _savedTrailMaxPoints;
        private float _savedTrailMinPointDistance;
        private bool _savedTrailLoops;
        private FloatParameterTrack _savedTrailWidthOverLength;
        private FloatParameterTrack _savedTrailAlphaOverLength;
        private FloatParameterTrack _savedTrailWidthOverTime;
        private FloatParameterTrack _savedTrailAlphaOverTime;
        private FloatParameterTrack _savedTrailDuration;
        private TodParticleDefinition _particleDefinition;
        private TodParticleDefinition _savedParticleDefinition;
        private string _selectedReanimLayer;
        private ReanimTrackViewModel _selectedReanimTrack;
        private int _selectedReanimFrameIndex;
        private double _reanimFps = 12d;
        private bool _reanimIsPlaying;
        private bool _isViewportPanModeEnabled;
        private bool _selectedReanimFrameVisible;
        private string _selectedReanimImageId = string.Empty;
        private string _selectedReanimFontId = string.Empty;
        private string _selectedReanimText = string.Empty;
        private bool _isReanimTransformDialogOpen;
        private ParticleEmitterViewModel _selectedParticleEmitter;
        private string _trailImageId;
        private int _trailMaxPoints;
        private double _trailMinPointDistance;
        private bool _trailLoops;
        private string _trailDefinitionError;
        private string _reanimDefinitionError;
        private string _particleDefinitionError;
        private bool _suppressReanimPropertyChanges;
        private bool _suppressReanimTweenSelection;
        private bool _suppressTrailPropertyChanges;
        private bool _suppressParticlePropertyChanges;
        private int _reanimTimelineRevision;

        [ObservableProperty]
        private string _assetId;

        private string _savedAssetId;
        public string Path { get; }
        public string EditorSummary { get; }
        public EffectFileSummary FileSummary { get; }
        public ObservableCollection<string> ReanimLayers { get; } = [];
        public ObservableCollection<ReanimTrackViewModel> ReanimTracks { get; } = [];
        public ObservableCollection<ReanimTweenViewModel> ReanimTweens { get; } = [];
        public int ReanimTimelineRevision => _reanimTimelineRevision;
        public ObservableCollection<ParticleEmitterViewModel> ParticleEmitters { get; } = [];
        public bool IsReanimEditor => Kind == EffectAssetKind.Reanim;
        public bool IsParticleEditor => Kind == EffectAssetKind.Particle;
        public bool IsTrailEditor => Kind == EffectAssetKind.Trail;
        public bool HasReanimControls => Kind == EffectAssetKind.Reanim && ReanimTracks.Count > 0;
        public bool HasSelectedReanimFrame => IsReanimEditor && SelectedReanimTrack is not null && ReanimFrameCount > 0;
        public bool IsSelectedReanimFrameTweened => SelectedReanimTrack is not null &&
            IsFrameTweened(SelectedReanimTrack.Index, SelectedReanimFrameIndex);
        public bool CanTransformSelectedReanimFrame => HasSelectedReanimFrame && !IsSelectedReanimFrameTweened;
        public bool CanDrag => CanTransformSelectedReanimFrame && SelectedReanimFrameVisible;
        public bool IsPanModeEnabled => IsViewportPanModeEnabled;
        public bool CanRemoveReanimTrack => IsReanimEditor && ReanimTracks.Count > 1 && SelectedReanimTrack is not null;
        public bool CanRemoveReanimFrame => IsReanimEditor && ReanimFrameCount > 1;
        public bool HasReanimTweenMetadata => ReanimTweenCount > 0;
        public bool HasNoReanimTweenMetadata => !HasReanimTweenMetadata;
        public int ReanimTweenCount => _reanimAsset?.Tweens?.Count ?? 0;
        public bool HasSelectedReanimTween => SelectedReanimTween is not null;
        public bool CanMakeSelectedReanimFrameKeyframe => SelectedReanimTrack is not null &&
            _reanimAsset?.Tweens is not null &&
            _reanimAsset.Tweens.Any(tween =>
                tween.TrackIndex == SelectedReanimTrack.Index &&
                SelectedReanimFrameIndex > tween.StartFrame &&
                SelectedReanimFrameIndex < tween.EndFrame);
        public bool CanEditReanimTweens => IsReanimEditor && _reanimAsset is not null && ReanimTrackCount > 0 && ReanimFrameCount > 2;
        public int ReanimTrackCount => _reanimDefinition?.mTrackCount ?? 0;
        public int ReanimFrameCount => _reanimDefinition?.mTracks is null || _reanimDefinition.mTrackCount <= 0
            ? 0
            : _reanimDefinition.mTracks.Take(_reanimDefinition.mTrackCount).Max(track => track?.mTransformCount ?? 0);
        public string ReanimDurationSummary => ReanimFrameCount == 0 || ReanimFps <= 0d
            ? "0.000 s"
            : $"{ReanimFrameCount / ReanimFps:0.###} s";
        public string SelectedReanimFrameSummary => HasSelectedReanimFrame
            ? $"{SelectedReanimTrack.DisplayName} / Frame {SelectedReanimFrameIndex + 1}"
            : "No frame selected";
        public ReanimTransformDialogViewModel ReanimTransformDialog { get; }
        private ReanimTweenViewModel _selectedReanimTween;
        public ReanimTweenViewModel SelectedReanimTween
        {
            get => _selectedReanimTween;
            set
            {
                if (SetProperty(ref _selectedReanimTween, value))
                {
                    if (!_suppressReanimTweenSelection && value is not null)
                    {
                        try
                        {
                            _suppressReanimTweenSelection = true;
                            SelectReanimTimelineCell(value.Model.TrackIndex, value.Model.StartFrame);
                        }
                        finally
                        {
                            _suppressReanimTweenSelection = false;
                        }
                    }

                    OnPropertyChanged(nameof(HasSelectedReanimTween));
                    OnPropertyChanged(nameof(CanMakeSelectedReanimFrameKeyframe));
                    OnPropertyChanged(nameof(SelectedReanimTweenSummary));
                }
            }
        }

        public string SelectedReanimTweenSummary => SelectedReanimTween is null
            ? "No tween selected"
            : $"{SelectedReanimTween.TrackName} / Frames {SelectedReanimTween.StartFrameNumber:0}-{SelectedReanimTween.EndFrameNumber:0}";
        public bool HasParticleControls => Kind == EffectAssetKind.Particle && _particleDefinition is not null;
        public bool HasTrailControls => Kind == EffectAssetKind.Trail && _trailDefinition is not null;
        public bool CanRemoveParticleEmitter => SelectedParticleEmitter is not null;
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
        public FloatParameterTrackViewModel TrailWidthOverTime { get; } = new("WidthOverTime", TrailDefaultWidthOverTime);
        public FloatParameterTrackViewModel TrailAlphaOverTime { get; } = new("AlphaOverTime", TrailDefaultAlphaOverTime);
        public FloatParameterTrackViewModel TrailDuration { get; } = new("TrailDuration", TrailDefaultDuration);

        public double ReanimFps
        {
            get => _reanimFps;
            set
            {
                double clamped = System.Math.Clamp(value, 1d, 240d);
                if (SetProperty(ref _reanimFps, clamped))
                {
                    ApplyReanimPropertyChanges();
                    OnPropertyChanged(nameof(ReanimDurationSummary));
                }
            }
        }

        public bool ReanimIsPlaying
        {
            get => _reanimIsPlaying;
            set
            {
                if (SetProperty(ref _reanimIsPlaying, value))
                {
                    ApplyReanimPreviewState();
                }
            }
        }

        public bool IsViewportPanModeEnabled
        {
            get => _isViewportPanModeEnabled;
            set => SetProperty(ref _isViewportPanModeEnabled, value);
        }

        public ReanimTrackViewModel SelectedReanimTrack
        {
            get => _selectedReanimTrack;
            set
            {
                if (SetProperty(ref _selectedReanimTrack, value))
                {
                    UpdateSelectedReanimTrackState();
                    UpdateReanimTimelineSelection();
                    LoadSelectedReanimFrame();
                }
            }
        }

        public int SelectedReanimFrameIndex
        {
            get => _selectedReanimFrameIndex;
            set
            {
                int clamped = System.Math.Clamp(value, 0, System.Math.Max(0, ReanimFrameCount - 1));
                if (SetProperty(ref _selectedReanimFrameIndex, clamped))
                {
                    UpdateReanimTimelineSelection();
                    LoadSelectedReanimFrame();
                    ApplyReanimPreviewState();
                }
            }
        }

        public bool SelectedReanimFrameVisible
        {
            get => _selectedReanimFrameVisible;
            set
            {
                if (SetProperty(ref _selectedReanimFrameVisible, value))
                {
                    ApplyReanimFrameResourceChanges();
                }
            }
        }

        public string SelectedReanimImageId
        {
            get => _selectedReanimImageId;
            set
            {
                if (SetProperty(ref _selectedReanimImageId, value ?? string.Empty))
                {
                    ApplyReanimFrameResourceChanges();
                }
            }
        }

        public string SelectedReanimFontId
        {
            get => _selectedReanimFontId;
            set
            {
                if (SetProperty(ref _selectedReanimFontId, value ?? string.Empty))
                {
                    ApplyReanimFrameResourceChanges();
                }
            }
        }

        public string SelectedReanimText
        {
            get => _selectedReanimText;
            set
            {
                if (SetProperty(ref _selectedReanimText, value ?? string.Empty))
                {
                    ApplyReanimFrameResourceChanges();
                }
            }
        }

        public bool IsReanimTransformDialogOpen
        {
            get => _isReanimTransformDialogOpen;
            set => SetProperty(ref _isReanimTransformDialogOpen, value);
        }

        public string ReanimDefinitionError
        {
            get => _reanimDefinitionError;
            private set => SetProperty(ref _reanimDefinitionError, value);
        }

        public bool HasReanimDefinitionError => !string.IsNullOrWhiteSpace(ReanimDefinitionError);

        public string TrailDefinitionError
        {
            get => _trailDefinitionError;
            private set => SetProperty(ref _trailDefinitionError, value);
        }

        public bool HasTrailDefinitionError => !string.IsNullOrWhiteSpace(TrailDefinitionError);

        public string ParticleDefinitionError
        {
            get => _particleDefinitionError;
            private set => SetProperty(ref _particleDefinitionError, value);
        }

        public bool HasParticleDefinitionError => !string.IsNullOrWhiteSpace(ParticleDefinitionError);

        public ParticleEmitterViewModel SelectedParticleEmitter
        {
            get => _selectedParticleEmitter;
            set
            {
                if (SetProperty(ref _selectedParticleEmitter, value))
                {
                    OnPropertyChanged(nameof(CanRemoveParticleEmitter));
                }
            }
        }

        public override bool SupportsSave => Kind == EffectAssetKind.Reanim || Kind == EffectAssetKind.Trail || Kind == EffectAssetKind.Particle;
        public override bool SupportsFileExport => true;
        public override string ExportPath => Path;

        public override Task ExportAsync(EffectProjectService projectService, EffectProject project, Stream outputStream, string targetFileName)
        {
            if (Kind == EffectAssetKind.Reanim)
            {
                ReanimatorDefinition definition = _reanimDefinition ?? LoadReanimDefinitionFromFile();
                NormalizeReanimDefinition(definition);
                ReanimReader.Encode(outputStream, definition, targetFileName);
                return Task.CompletedTask;
            }

            if (Kind == EffectAssetKind.Particle)
            {
                TodParticleDefinition definition = _particleDefinition ?? LoadParticleDefinitionFromFile();
                if (_particleDefinition is not null && !TryApplyParticleEmitters())
                {
                    throw new InvalidDataException(ParticleDefinitionError);
                }

                SexyParticleReader.Encode(outputStream, definition, targetFileName);
                return Task.CompletedTask;
            }

            if (Kind == EffectAssetKind.Trail)
            {
                TrailDefinition definition = _trailDefinition ?? new TrailDefinition();
                if (_trailDefinition is not null && !TryApplyTrailTracks())
                {
                    throw new InvalidDataException(TrailDefinitionError);
                }

                TrailReader.Encode(outputStream, definition, targetFileName);
                return Task.CompletedTask;
            }

            return base.ExportAsync(projectService, project, outputStream, targetFileName);
        }

        public string SelectedReanimLayer
        {
            get => _selectedReanimLayer;
            set
            {
                if (SetProperty(ref _selectedReanimLayer, value))
                {
                    ApplyReanimPreviewState();
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
            _assetId = assetId;
            _savedAssetId = assetId;
            Path = path;
            ReanimTransformDialog = new ReanimTransformDialogViewModel(ApplyTransformDialogChanges, CloseReanimTransformDialog);
            FileSummary = new EffectFileAnalyzer().Analyze(project, kind, path);
            TrailWidthOverLength.Changed += OnTrailTrackChanged;
            TrailAlphaOverLength.Changed += OnTrailTrackChanged;
            TrailWidthOverTime.Changed += OnTrailTrackChanged;
            TrailAlphaOverTime.Changed += OnTrailTrackChanged;
            TrailDuration.Changed += OnTrailTrackChanged;
            EditorSummary = kind switch
            {
                EffectAssetKind.Reanim => "Reanim editor shell: tracks, frames, transforms, and image bindings will live here.",
                EffectAssetKind.Particle => "Particle editor shell: emitter list, parameter tracks, fields, and live preview are available here.",
                EffectAssetKind.Trail => "Trail editor shell: width, alpha, duration, point settings, and path preview will live here.",
                _ => "Effect editor shell."
            };
            if (kind == EffectAssetKind.Reanim)
            {
                _project.Assets.Reanims.TryGetValue(assetId, out _reanimAsset);
                _reanimPreview = new ReanimPreviewSimulation(project, path);
                PreviewFrameProvider = _reanimPreview;
                PreviewFrame = EffectPreviewFrameBuilder.BuildPlaceholder(kind, assetId);
                InitializeReanimControls();
            }
            else if (kind == EffectAssetKind.Particle)
            {
                PreviewFrame = EffectPreviewFrameBuilder.BuildPlaceholder(kind, assetId);
                InitializeParticleEditor();
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

        partial void OnAssetIdChanged(string value)
        {
            string normalizedId = NormalizeEditedAssetId(value);
            if (string.IsNullOrWhiteSpace(normalizedId))
            {
                normalizedId = GetManifestAsset()?.Id ?? _savedAssetId;
            }

            if (!string.Equals(value, normalizedId, System.StringComparison.Ordinal))
            {
                AssetId = normalizedId;
                return;
            }

            EffectAsset asset = GetManifestAsset();
            if (asset is null || string.Equals(asset.Id, normalizedId, System.StringComparison.Ordinal))
            {
                return;
            }

            string uniqueId = CreateUniqueAssetId(normalizedId);
            if (!string.Equals(normalizedId, uniqueId, System.StringComparison.Ordinal))
            {
                AssetId = uniqueId;
                return;
            }

            asset.Id = uniqueId;
            Title = uniqueId;
            DocumentId = CreateDocumentId(Kind, uniqueId);
            _project?.RebuildAssetIndex();
            RefreshPreviewForAssetId();
            MarkDirty();
            OnPropertyChanged(nameof(AssetId));
        }

        private static string NormalizeEditedAssetId(string assetId)
        {
            return string.IsNullOrWhiteSpace(assetId)
                ? string.Empty
                : assetId.Trim();
        }

        private string CreateUniqueAssetId(string assetId)
        {
            string candidate = assetId;
            for (int i = 2; AssetIdExists(candidate); i++)
            {
                candidate = $"{assetId}_{i}";
            }

            return candidate;
        }

        private bool AssetIdExists(string assetId)
        {
            EffectAsset current = GetManifestAsset();
            return GetManifestAssets().Any(asset =>
                !ReferenceEquals(asset, current) &&
                string.Equals(asset.Id, assetId, System.StringComparison.OrdinalIgnoreCase));
        }

        private EffectAsset GetManifestAsset()
        {
            return Kind switch
            {
                EffectAssetKind.Reanim => _project?.Manifest?.Reanims?.FirstOrDefault(asset => string.Equals(asset.Path, Path, System.StringComparison.OrdinalIgnoreCase)),
                EffectAssetKind.Particle => _project?.Manifest?.Particles?.FirstOrDefault(asset => string.Equals(asset.Path, Path, System.StringComparison.OrdinalIgnoreCase)),
                EffectAssetKind.Trail => _project?.Manifest?.Trails?.FirstOrDefault(asset => string.Equals(asset.Path, Path, System.StringComparison.OrdinalIgnoreCase)),
                _ => null
            };
        }

        private IEnumerable<EffectAsset> GetManifestAssets()
        {
            return Kind switch
            {
                EffectAssetKind.Reanim => _project?.Manifest?.Reanims ?? [],
                EffectAssetKind.Particle => _project?.Manifest?.Particles ?? [],
                EffectAssetKind.Trail => _project?.Manifest?.Trails ?? [],
                _ => []
            };
        }

        private void RefreshPreviewForAssetId()
        {
            if (Kind == EffectAssetKind.Trail && _trailDefinition is not null)
            {
                RefreshTrailPreview();
            }
            else if (Kind == EffectAssetKind.Particle && _particleDefinition is not null)
            {
                RefreshParticlePreview();
            }
            else
            {
                PreviewFrame = EffectPreviewFrameBuilder.BuildPlaceholder(Kind, AssetId);
            }
        }

        [RelayCommand]
        private void AddReanimTrack()
        {
            if (Kind != EffectAssetKind.Reanim || _reanimDefinition is null)
            {
                return;
            }

            ReanimatorTrack[] tracks = _reanimDefinition.mTracks ?? [];
            int frameCount = System.Math.Max(1, ReanimFrameCount);
            System.Array.Resize(ref tracks, tracks.Length + 1);
            tracks[^1] = new ReanimatorTrack($"track_{tracks.Length}", frameCount);
            for (int i = 0; i < frameCount; i++)
            {
                tracks[^1].mTransforms[i] = CreateDefaultReanimTransform(i == 0);
            }

            _reanimDefinition.mTracks = tracks;
            _reanimDefinition.mTrackCount = (short)tracks.Length;
            NormalizeReanimDefinition(_reanimDefinition);
            NormalizeReanimTweenMetadata();
            RebuildReanimTweenViewModels(SelectedReanimTween?.Index ?? 0);
            RebuildReanimViewModels(tracks.Length - 1, SelectedReanimFrameIndex);
            ApplyReanimPropertyChanges();
        }

        [RelayCommand]
        private void RemoveReanimTrack()
        {
            if (Kind != EffectAssetKind.Reanim ||
                _reanimDefinition?.mTracks is null ||
                SelectedReanimTrack is null ||
                _reanimDefinition.mTrackCount <= 1)
            {
                return;
            }

            int removeIndex = SelectedReanimTrack.Index;
            int count = System.Math.Min(_reanimDefinition.mTrackCount, _reanimDefinition.mTracks.Length);
            if (removeIndex < 0 || removeIndex >= count)
            {
                return;
            }

            ReanimatorTrack[] tracks = new ReanimatorTrack[count - 1];
            int target = 0;
            for (int i = 0; i < count; i++)
            {
                if (i != removeIndex)
                {
                    tracks[target++] = _reanimDefinition.mTracks[i];
                }
            }

            _reanimDefinition.mTracks = tracks;
            _reanimDefinition.mTrackCount = (short)tracks.Length;
            RemoveReanimTweenTrack(removeIndex);
            NormalizeReanimDefinition(_reanimDefinition);
            NormalizeReanimTweenMetadata();
            RebuildReanimTweenViewModels(System.Math.Min(SelectedReanimTween?.Index ?? 0, ReanimTweenCount - 1));
            RebuildReanimViewModels(System.Math.Min(removeIndex, tracks.Length - 1), SelectedReanimFrameIndex);
            ApplyReanimPropertyChanges();
        }

        [RelayCommand]
        private void AddReanimFrame()
        {
            if (Kind != EffectAssetKind.Reanim || _reanimDefinition?.mTracks is null)
            {
                return;
            }

            int insertIndex = System.Math.Clamp(SelectedReanimFrameIndex + 1, 0, ReanimFrameCount);
            int count = System.Math.Min(_reanimDefinition.mTrackCount, _reanimDefinition.mTracks.Length);
            for (int i = 0; i < count; i++)
            {
                InsertTransform(_reanimDefinition.mTracks[i], insertIndex, CreateDefaultReanimTransform(false));
            }

            InsertReanimTweenFrame(insertIndex);
            NormalizeReanimDefinition(_reanimDefinition);
            NormalizeReanimTweenMetadata();
            ApplyAllReanimTweens();
            RebuildReanimTweenViewModels(SelectedReanimTween?.Index ?? 0);
            RebuildReanimViewModels(SelectedReanimTrack?.Index ?? 0, insertIndex);
            ApplyReanimPropertyChanges();
        }

        [RelayCommand]
        private void RemoveReanimFrame()
        {
            if (Kind != EffectAssetKind.Reanim ||
                _reanimDefinition?.mTracks is null ||
                ReanimFrameCount <= 1)
            {
                return;
            }

            int removeIndex = System.Math.Clamp(SelectedReanimFrameIndex, 0, ReanimFrameCount - 1);
            int count = System.Math.Min(_reanimDefinition.mTrackCount, _reanimDefinition.mTracks.Length);
            for (int i = 0; i < count; i++)
            {
                RemoveTransform(_reanimDefinition.mTracks[i], removeIndex);
            }

            RemoveReanimTweenFrame(removeIndex);
            NormalizeReanimDefinition(_reanimDefinition);
            NormalizeReanimTweenMetadata();
            ApplyAllReanimTweens();
            RebuildReanimTweenViewModels(System.Math.Min(SelectedReanimTween?.Index ?? 0, ReanimTweenCount - 1));
            RebuildReanimViewModels(SelectedReanimTrack?.Index ?? 0, System.Math.Min(removeIndex, ReanimFrameCount - 1));
            ApplyReanimPropertyChanges();
        }

        [RelayCommand]
        private void RecognizeReanimTweens()
        {
            if (Kind != EffectAssetKind.Reanim || _reanimDefinition?.mTracks is null || _reanimAsset is null)
            {
                return;
            }

            _reanimAsset.Tweens = InferReanimTweens(_reanimDefinition);
            NormalizeReanimTweenMetadata();
            RebuildReanimTweenViewModels(0);
            RefreshReanimTimelineCells();
            RaiseReanimTweenPropertiesChanged();
            MarkDirty();
        }

        [RelayCommand]
        private void AddReanimTween()
        {
            if (!CanEditReanimTweens || _reanimAsset is null)
            {
                return;
            }

            _reanimAsset.Tweens ??= [];
            int trackIndex = System.Math.Clamp(SelectedReanimTrack?.Index ?? 0, 0, System.Math.Max(0, ReanimTrackCount - 1));
            int startFrame = System.Math.Clamp(SelectedReanimFrameIndex, 0, System.Math.Max(0, ReanimFrameCount - 3));
            int endFrame = System.Math.Min(ReanimFrameCount - 1, startFrame + 2);
            ReanimTween tween = new()
            {
                TrackIndex = trackIndex,
                TrackName = GetReanimTrackName(trackIndex),
                StartFrame = startFrame,
                EndFrame = endFrame,
                Properties = ReanimTween.CreateTweenedProperties()
            };

            _reanimAsset.Tweens.Add(tween);
            NormalizeReanimTweenMetadata();
            ApplyReanimTween(tween);
            int selectedIndex = System.Math.Max(0, _reanimAsset.Tweens.IndexOf(tween));
            RebuildReanimTweenViewModels(selectedIndex);
            RefreshReanimTimelineCells();
            RaiseReanimTweenPropertiesChanged();
            ApplyReanimPropertyChanges();
        }

        [RelayCommand]
        private void RemoveReanimTween()
        {
            if (SelectedReanimTween is null || _reanimAsset?.Tweens is null)
            {
                return;
            }

            int removeIndex = ReanimTweens.IndexOf(SelectedReanimTween);
            if (removeIndex < 0 || removeIndex >= _reanimAsset.Tweens.Count)
            {
                return;
            }

            _reanimAsset.Tweens.RemoveAt(removeIndex);
            RebuildReanimTweenViewModels(System.Math.Min(removeIndex, _reanimAsset.Tweens.Count - 1));
            RefreshReanimTimelineCells();
            RaiseReanimTweenPropertiesChanged();
            ApplyReanimPropertyChanges();
        }

        [RelayCommand]
        private void BakeSelectedReanimTween()
        {
            if (SelectedReanimTween is null)
            {
                return;
            }

            BakeReanimTween(SelectedReanimTween.Model, removeTween: true);
        }

        [RelayCommand]
        private void BakeAllReanimTweens()
        {
            if (_reanimAsset?.Tweens is null || _reanimAsset.Tweens.Count == 0)
            {
                return;
            }

            List<ReanimTween> tweens = CloneReanimTweens(_reanimAsset.Tweens);
            foreach (ReanimTween tween in tweens)
            {
                ApplyReanimTween(tween);
            }

            _reanimAsset.Tweens.Clear();
            RebuildReanimTweenViewModels(-1);
            RefreshReanimTimelineCells();
            RaiseReanimTweenPropertiesChanged();
            ApplyReanimPropertyChanges();
        }

        [RelayCommand]
        private void MakeSelectedReanimFrameKeyframe()
        {
            if (SelectedReanimTrack is null || _reanimAsset?.Tweens is null)
            {
                return;
            }

            int trackIndex = SelectedReanimTrack.Index;
            int frameIndex = SelectedReanimFrameIndex;
            List<ReanimTween> containingTweens = _reanimAsset.Tweens
                .Where(tween => tween.TrackIndex == trackIndex &&
                    frameIndex > tween.StartFrame &&
                    frameIndex < tween.EndFrame)
                .ToList();
            if (containingTweens.Count == 0)
            {
                return;
            }

            foreach (ReanimTween tween in containingTweens)
            {
                BakeReanimTween(tween, removeTween: true, refresh: false, markDirty: false);
                AddSplitReanimTween(tween, tween.StartFrame, frameIndex);
                AddSplitReanimTween(tween, frameIndex, tween.EndFrame);
            }

            NormalizeReanimTweenMetadata();
            ApplyAllReanimTweens();
            int selectedTweenIndex = _reanimAsset.Tweens.FindIndex(tween =>
                tween.TrackIndex == trackIndex &&
                tween.StartFrame == frameIndex);
            if (selectedTweenIndex < 0)
            {
                selectedTweenIndex = _reanimAsset.Tweens.FindIndex(tween =>
                    tween.TrackIndex == trackIndex &&
                    tween.EndFrame == frameIndex);
            }

            RebuildReanimTweenViewModels(selectedTweenIndex);
            RefreshReanimTimelineCells();
            RaiseReanimTweenPropertiesChanged();
            ApplyReanimPropertyChanges();
        }

        [RelayCommand]
        private void OpenReanimTransformDialog()
        {
            if (!CanTransformSelectedReanimFrame)
            {
                return;
            }

            LoadTransformDialog();
            IsReanimTransformDialogOpen = true;
        }

        [RelayCommand]
        private void AddParticleEmitter()
        {
            if (Kind != EffectAssetKind.Particle || _particleDefinition is null)
            {
                return;
            }

            if (!TryApplyParticleEmitters())
            {
                return;
            }

            TodEmitterDefinition[] emitters = _particleDefinition.mEmitterDefs ?? [];
            int index = _particleDefinition.mEmitterDefCount;
            System.Array.Resize(ref emitters, index + 1);
            emitters[index] = CreateDefaultEmitter(index);
            _particleDefinition.mEmitterDefs = emitters;
            _particleDefinition.mEmitterDefCount = emitters.Length;
            RebuildParticleEmitterViewModels(index);
            ApplyParticlePropertyChanges();
        }

        [RelayCommand]
        private void RemoveParticleEmitter()
        {
            if (Kind != EffectAssetKind.Particle ||
                _particleDefinition?.mEmitterDefs is null ||
                SelectedParticleEmitter is null)
            {
                return;
            }

            if (!TryApplyParticleEmitters())
            {
                return;
            }

            int removeIndex = SelectedParticleEmitter.Index;
            int count = System.Math.Min(_particleDefinition.mEmitterDefCount, _particleDefinition.mEmitterDefs.Length);
            if (removeIndex < 0 || removeIndex >= count)
            {
                return;
            }

            TodEmitterDefinition[] emitters = new TodEmitterDefinition[count - 1];
            int targetIndex = 0;
            for (int i = 0; i < count; i++)
            {
                if (i == removeIndex)
                {
                    continue;
                }

                emitters[targetIndex++] = _particleDefinition.mEmitterDefs[i];
            }

            _particleDefinition.mEmitterDefs = emitters;
            _particleDefinition.mEmitterDefCount = emitters.Length;
            RebuildParticleEmitterViewModels(System.Math.Min(removeIndex, emitters.Length - 1));
            ApplyParticlePropertyChanges();
        }

        private void InitializeReanimControls()
        {
            if (_reanimPreview is null)
            {
                return;
            }

            _reanimDefinition = LoadReanimDefinitionFromFile();
            NormalizeReanimDefinition(_reanimDefinition);
            NormalizeReanimTweenMetadata();
            RebuildReanimTweenViewModels(0);
            _suppressReanimPropertyChanges = true;
            ReanimFps = _reanimDefinition.mFPS <= 0f ? 12d : _reanimDefinition.mFPS;
            ReanimIsPlaying = false;
            _suppressReanimPropertyChanges = false;
            RebuildReanimViewModels(0, 0);
            ApplyReanimPreviewState();
            AcceptSavedState();
            OnPropertyChanged(nameof(HasReanimControls));
        }

        private void OnReanimTrackVisibilityChanged(ReanimTrackViewModel track)
        {
            _reanimPreview?.SetTrackVisible(track.Index, track.IsVisible);
        }

        private void OnReanimTrackNameChanged(ReanimTrackViewModel track)
        {
            if (_reanimDefinition?.mTracks is null ||
                track.Index < 0 ||
                track.Index >= _reanimDefinition.mTrackCount)
            {
                return;
            }

            _reanimDefinition.mTracks[track.Index].mName = track.Name ?? string.Empty;
            UpdateReanimTweenTrackName(track.Index, track.Name);
            RefreshReanimTweenTrackNames();
            RefreshReanimLayers();
            ApplyReanimPropertyChanges();
        }

        private void OnTrailTrackChanged(FloatParameterTrackViewModel track)
        {
            ApplyTrailPropertyChanges();
        }

        private void OnParticleEmitterChanged(ParticleEmitterViewModel emitter)
        {
            ApplyParticlePropertyChanges();
        }

        public override async Task SaveAsync(EffectProjectService projectService, EffectProject project)
        {
            if (string.IsNullOrWhiteSpace(AssetId))
            {
                throw new InvalidOperationException("Asset ID cannot be empty.");
            }

            if (Kind == EffectAssetKind.Reanim && _reanimDefinition is not null)
            {
                string reanimFullPath = ResolveEffectPath(project, Path, project.Assets.Reanims.TryGetValue(AssetId, out ReanimAsset asset) ? asset : null);
                if (string.IsNullOrWhiteSpace(reanimFullPath))
                {
                    return;
                }

                string reanimDirectory = System.IO.Path.GetDirectoryName(reanimFullPath);
                if (!string.IsNullOrWhiteSpace(reanimDirectory))
                {
                    Directory.CreateDirectory(reanimDirectory);
                }

                NormalizeReanimDefinition(_reanimDefinition);
                NormalizeReanimTweenMetadata();
                await using FileStream reanimStream = File.Create(reanimFullPath);
                ReanimReader.Encode(reanimStream, _reanimDefinition, reanimFullPath);
                await projectService.SaveAsync(project);
                AcceptSavedState();
                return;
            }

            if (Kind == EffectAssetKind.Particle && _particleDefinition is not null)
            {
                string particleFullPath = ResolveEffectPath(project, Path, project.Assets.Particles.TryGetValue(AssetId, out EffectAsset asset) ? asset : null);
                if (string.IsNullOrWhiteSpace(particleFullPath))
                {
                    return;
                }

                string particleDirectory = System.IO.Path.GetDirectoryName(particleFullPath);
                if (!string.IsNullOrWhiteSpace(particleDirectory))
                {
                    Directory.CreateDirectory(particleDirectory);
                }

                if (!TryApplyParticleEmitters())
                {
                    throw new InvalidDataException(ParticleDefinitionError);
                }

                await using FileStream particleStream = File.Create(particleFullPath);
                SexyParticleReader.Encode(particleStream, _particleDefinition, particleFullPath);
                AcceptSavedState();
                return;
            }

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
            TrailReader.Encode(stream, _trailDefinition, fullPath);
            AcceptSavedState();
        }

        public override void AcceptSavedState()
        {
            _savedAssetId = AssetId;

            if (Kind == EffectAssetKind.Reanim && _reanimDefinition is not null)
            {
                NormalizeReanimDefinition(_reanimDefinition);
                NormalizeReanimTweenMetadata();
                _savedReanimDefinition = CloneReanimDefinition(_reanimDefinition);
                _savedReanimTweens = CloneReanimTweens(_reanimAsset?.Tweens);
            }
            else if (Kind == EffectAssetKind.Trail && _trailDefinition is not null)
            {
                _savedTrailImageId = TrailImageId;
                _savedTrailMaxPoints = TrailMaxPoints;
                _savedTrailMinPointDistance = (float)TrailMinPointDistance;
                _savedTrailLoops = TrailLoops;
                _savedTrailWidthOverLength = CloneTrack(_trailDefinition.mWidthOverLength);
                _savedTrailAlphaOverLength = CloneTrack(_trailDefinition.mAlphaOverLength);
                _savedTrailWidthOverTime = CloneTrack(_trailDefinition.mWidthOverTime);
                _savedTrailAlphaOverTime = CloneTrack(_trailDefinition.mAlphaOverTime);
                _savedTrailDuration = CloneTrack(_trailDefinition.mTrailDuration);
            }
            else if (Kind == EffectAssetKind.Particle && _particleDefinition is not null)
            {
                TryApplyParticleEmitters();
                _savedParticleDefinition = ParticleDefinitionUtility.Clone(_particleDefinition);
            }

            base.AcceptSavedState();
        }

        public override void DiscardChanges()
        {
            if (!string.Equals(AssetId, _savedAssetId, System.StringComparison.Ordinal))
            {
                EffectAsset asset = GetManifestAsset();
                if (asset is not null)
                {
                    asset.Id = _savedAssetId;
                }

                AssetId = _savedAssetId;
                Title = _savedAssetId;
                DocumentId = CreateDocumentId(Kind, _savedAssetId);
                _project?.RebuildAssetIndex();
                RefreshPreviewForAssetId();
            }

            if (Kind == EffectAssetKind.Reanim && _savedReanimDefinition is not null)
            {
                _reanimDefinition = CloneReanimDefinition(_savedReanimDefinition);
                NormalizeReanimDefinition(_reanimDefinition);
                if (_reanimAsset is not null)
                {
                    _reanimAsset.Tweens = CloneReanimTweens(_savedReanimTweens);
                    NormalizeReanimTweenMetadata();
                }
                RebuildReanimTweenViewModels(System.Math.Min(SelectedReanimTween?.Index ?? 0, ReanimTweenCount - 1));

                ReanimFps = _reanimDefinition.mFPS <= 0f ? 12d : _reanimDefinition.mFPS;
                RebuildReanimViewModels(
                    System.Math.Min(SelectedReanimTrack?.Index ?? 0, System.Math.Max(0, _reanimDefinition.mTrackCount - 1)),
                    System.Math.Min(SelectedReanimFrameIndex, System.Math.Max(0, ReanimFrameCount - 1)));
                ApplyReanimPreviewState();
                RaiseReanimTweenPropertiesChanged();
            }
            else if (Kind == EffectAssetKind.Trail && _trailDefinition is not null)
            {
                SetTrailProperties(
                    _savedTrailImageId,
                    _savedTrailMaxPoints,
                    _savedTrailMinPointDistance,
                    _savedTrailLoops,
                    _savedTrailWidthOverLength,
                    _savedTrailAlphaOverLength,
                    _savedTrailWidthOverTime,
                    _savedTrailAlphaOverTime,
                    _savedTrailDuration,
                    markDirty: false);
            }
            else if (Kind == EffectAssetKind.Particle && _savedParticleDefinition is not null)
            {
                LoadParticleDefinition(ParticleDefinitionUtility.Clone(_savedParticleDefinition), markDirty: false);
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
                _trailDefinition.mWidthOverTime,
                _trailDefinition.mAlphaOverTime,
                _trailDefinition.mTrailDuration,
                markDirty: false);
            AcceptSavedState();
            RefreshTrailPreview();
            OnPropertyChanged(nameof(HasTrailControls));
        }

        private void InitializeParticleEditor()
        {
            TodParticleDefinition definition = LoadParticleDefinitionFromFile();
            LoadParticleDefinition(definition, markDirty: false);
            AcceptSavedState();
            RefreshParticlePreview();
            OnPropertyChanged(nameof(HasParticleControls));
        }

        private TodParticleDefinition LoadParticleDefinitionFromFile()
        {
            string fullPath = ResolveEffectPath(_project, Path, _project.Assets.Particles.TryGetValue(AssetId, out EffectAsset asset) ? asset : null);
            if (string.IsNullOrWhiteSpace(fullPath) || !File.Exists(fullPath))
            {
                return ParticleDefinitionUtility.CreateEmpty();
            }

            using FileStream stream = File.OpenRead(fullPath);
            return SexyParticleReader.Decode(stream) ?? ParticleDefinitionUtility.CreateEmpty();
        }

        private ReanimatorDefinition LoadReanimDefinitionFromFile()
        {
            string fullPath = ResolveEffectPath(_project, Path, _project.Assets.Reanims.TryGetValue(AssetId, out ReanimAsset asset) ? asset : null);
            if (string.IsNullOrWhiteSpace(fullPath) || !File.Exists(fullPath))
            {
                return CreateEmptyReanimDefinition();
            }

            using FileStream stream = File.OpenRead(fullPath);
            return ReanimReader.Decode(stream) ?? CreateEmptyReanimDefinition();
        }

        private void RebuildReanimViewModels(int selectedTrackIndex, int selectedFrameIndex)
        {
            foreach (ReanimTrackViewModel track in ReanimTracks)
            {
                track.VisibilityChanged -= OnReanimTrackVisibilityChanged;
                track.NameChanged -= OnReanimTrackNameChanged;
            }

            ReanimTracks.Clear();
            int frameCount = ReanimFrameCount;

            if (_reanimDefinition?.mTracks is not null)
            {
                int count = System.Math.Min(_reanimDefinition.mTrackCount, _reanimDefinition.mTracks.Length);
                for (int trackIndex = 0; trackIndex < count; trackIndex++)
                {
                    ReanimatorTrack definitionTrack = _reanimDefinition.mTracks[trackIndex];
                    ReanimTrackViewModel track = new(trackIndex, definitionTrack?.mName ?? string.Empty);
                    track.VisibilityChanged += OnReanimTrackVisibilityChanged;
                    track.NameChanged += OnReanimTrackNameChanged;
                    ReanimTracks.Add(track);
                }
            }

            RefreshReanimLayers();
            SelectedReanimFrameIndex = System.Math.Clamp(selectedFrameIndex, 0, System.Math.Max(0, frameCount - 1));
            SelectedReanimTrack = ReanimTracks.Count == 0
                ? null
                : ReanimTracks[System.Math.Clamp(selectedTrackIndex, 0, ReanimTracks.Count - 1)];
            UpdateSelectedReanimTrackState();
            UpdateReanimTimelineSelection();
            LoadSelectedReanimFrame();
            RaiseReanimTimelineChanged();
            RaiseReanimStructureChanged();
            RefreshReanimTweenTrackNames();
        }

        private void RefreshReanimLayers()
        {
            string previous = SelectedReanimLayer;
            ReanimLayers.Clear();
            ReanimLayers.Add("Full timeline");
            foreach (string layerName in BuildReanimLayerNames())
            {
                if (!ReanimLayers.Contains(layerName))
                {
                    ReanimLayers.Add(layerName);
                }
            }

            SelectedReanimLayer = !string.IsNullOrWhiteSpace(previous) && ReanimLayers.Contains(previous)
                ? previous
                : ReanimLayers.FirstOrDefault();
        }

        private IEnumerable<string> BuildReanimLayerNames()
        {
            if (_reanimDefinition?.mTracks is null || _reanimDefinition.mTrackCount <= 0)
            {
                return [];
            }

            string[] trackNames = _reanimDefinition.mTracks
                .Take(_reanimDefinition.mTrackCount)
                .Select(track => track?.mName)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .ToArray();
            string[] layerNames = trackNames
                .Where(name => name.StartsWith("anim_", System.StringComparison.OrdinalIgnoreCase))
                .ToArray();
            return layerNames.Length > 0 ? layerNames : trackNames;
        }

        private void SelectReanimFrame(int frameIndex)
        {
            SelectReanimTimelineFrame(frameIndex);
        }

        public void SelectReanimTimelineFrame(int frameIndex)
        {
            ReanimIsPlaying = false;
            SelectedReanimFrameIndex = frameIndex;
        }

        private void SelectReanimFrame(int trackIndex, int frameIndex)
        {
            SelectReanimTimelineCell(trackIndex, frameIndex);
        }

        public void SelectReanimTimelineCell(int trackIndex, int frameIndex)
        {
            ReanimIsPlaying = false;
            if (trackIndex >= 0 && trackIndex < ReanimTracks.Count)
            {
                SelectedReanimTrack = ReanimTracks[trackIndex];
            }

            SelectedReanimFrameIndex = frameIndex;
            SelectReanimTweenForCell(trackIndex, frameIndex);
        }

        public void ToggleReanimTimelineTrackVisibility(int trackIndex)
        {
            if (trackIndex < 0 || trackIndex >= ReanimTracks.Count)
            {
                return;
            }

            ReanimTracks[trackIndex].IsVisible = !ReanimTracks[trackIndex].IsVisible;
            RaiseReanimTimelineChanged();
        }

        private void SelectReanimTweenForCell(int trackIndex, int frameIndex)
        {
            if (_suppressReanimTweenSelection)
            {
                return;
            }

            ReanimTweenViewModel tween = ReanimTweens.FirstOrDefault(item =>
                item.Model.TrackIndex == trackIndex &&
                frameIndex >= item.Model.StartFrame &&
                frameIndex <= item.Model.EndFrame);
            if (tween is null || ReferenceEquals(tween, SelectedReanimTween))
            {
                return;
            }

            _suppressReanimTweenSelection = true;
            SelectedReanimTween = tween;
            _suppressReanimTweenSelection = false;
        }

        private void LoadSelectedReanimFrame()
        {
            ReanimatorTransform transform = new();
            bool canSelect = HasSelectedReanimFrame;
            if (canSelect)
            {
                canSelect = TryGetSelectedReanimTransform(out transform);
            }

            _suppressReanimPropertyChanges = true;
            if (canSelect)
            {
                SelectedReanimFrameVisible = transform.mFrame >= 0f;
                SelectedReanimImageId = transform.mImage ?? string.Empty;
                SelectedReanimFontId = transform.mFont ?? string.Empty;
                SelectedReanimText = transform.mText ?? string.Empty;
                LoadTransformDialog();
            }
            else
            {
                SelectedReanimFrameVisible = false;
                SelectedReanimImageId = string.Empty;
                SelectedReanimFontId = string.Empty;
                SelectedReanimText = string.Empty;
            }

            _suppressReanimPropertyChanges = false;
            OnPropertyChanged(nameof(HasSelectedReanimFrame));
            OnPropertyChanged(nameof(IsSelectedReanimFrameTweened));
            OnPropertyChanged(nameof(CanTransformSelectedReanimFrame));
            OnPropertyChanged(nameof(CanDrag));
            OnPropertyChanged(nameof(SelectedReanimFrameSummary));
        }

        private void ApplyReanimFrameResourceChanges()
        {
            if (_suppressReanimPropertyChanges || !CanTransformSelectedReanimFrame || !TryGetSelectedReanimTransform(out ReanimatorTransform transform))
            {
                return;
            }

            if (!SelectedReanimFrameVisible)
            {
                transform.mFrame = -1f;
            }
            else
            {
                NormalizeVisibleTransform(ref transform);
            }

            transform.mImage = string.IsNullOrWhiteSpace(SelectedReanimImageId) ? null : SelectedReanimImageId.Trim();
            transform.mFont = string.IsNullOrWhiteSpace(SelectedReanimFontId) ? null : SelectedReanimFontId.Trim();
            transform.mText = SelectedReanimText ?? string.Empty;
            SetSelectedReanimTransform(transform);
            RefreshSelectedFrameCell();
            LoadTransformDialog();
            OnPropertyChanged(nameof(CanTransformSelectedReanimFrame));
            OnPropertyChanged(nameof(CanDrag));
            ApplyReanimPropertyChanges();
        }

        private void SetSelectedReanimFrameResourceProperties(
            bool visible,
            string imageId,
            string fontId,
            string text)
        {
            _suppressReanimPropertyChanges = true;
            SelectedReanimFrameVisible = visible;
            SelectedReanimImageId = imageId ?? string.Empty;
            SelectedReanimFontId = fontId ?? string.Empty;
            SelectedReanimText = text ?? string.Empty;
            _suppressReanimPropertyChanges = false;
        }

        private void ApplyReanimPropertyChanges(bool markDirty = true)
        {
            if (_reanimDefinition is null || _suppressReanimPropertyChanges)
            {
                return;
            }

            _reanimDefinition.mFPS = (float)ReanimFps;
            NormalizeReanimDefinition(_reanimDefinition);
            ReanimDefinitionError = string.Empty;
            OnPropertyChanged(nameof(HasReanimDefinitionError));
            RefreshReanimLayers();
            ApplyReanimPreviewState();

            if (markDirty)
            {
                MarkDirty();
            }
        }

        private void ApplyReanimPreviewState()
        {
            if (_reanimPreview is null || _reanimDefinition is null)
            {
                return;
            }

            _reanimPreview.SetDefinition(_reanimDefinition);
            _reanimPreview.SetAnimRate((float)ReanimFps);
            _reanimPreview.SetLayer(SelectedReanimLayer == "Full timeline" ? null : SelectedReanimLayer);
            _reanimPreview.SetPaused(!ReanimIsPlaying);
            _reanimPreview.SetFrameIndex(SelectedReanimFrameIndex);
            ApplyReanimTrackVisibility();
        }

        private void ApplyReanimTrackVisibility()
        {
            foreach (ReanimTrackViewModel track in ReanimTracks)
            {
                _reanimPreview?.SetTrackVisible(track.Index, track.IsVisible);
            }
        }

        public void DragBy(Vector2 worldDelta)
        {
            if (!CanTransformSelectedReanimFrame || !TryGetSelectedReanimTransform(out ReanimatorTransform transform))
            {
                return;
            }

            transform.mTransX = (float)RoundToThreeDecimals(transform.mTransX + worldDelta.X);
            transform.mTransY = (float)RoundToThreeDecimals(transform.mTransY + worldDelta.Y);
            SetSelectedReanimTransform(transform);
            ApplyReanimTweensForSelection();
            RefreshReanimTimelineCells();
            LoadTransformDialog();
            ApplyReanimPropertyChanges();
        }

        private void LoadTransformDialog()
        {
            if (!TryGetSelectedReanimTransform(out ReanimatorTransform transform))
            {
                return;
            }

            ReanimTransformDialog.Load(
                SelectedReanimTrack?.Name ?? string.Empty,
                transform.mImage ?? string.Empty,
                transform.mFont ?? string.Empty,
                transform.mText ?? string.Empty,
                transform.mTransX,
                transform.mTransY,
                transform.mScaleX,
                transform.mScaleY,
                transform.mSkewX,
                transform.mSkewY,
                transform.mFrame,
                transform.mAlpha);
        }

        private void ApplyTransformDialogChanges()
        {
            if (_suppressReanimPropertyChanges || !CanTransformSelectedReanimFrame || !TryGetSelectedReanimTransform(out ReanimatorTransform transform))
            {
                return;
            }

            if (SelectedReanimTrack is not null && SelectedReanimTrack.Name != ReanimTransformDialog.TrackName)
            {
                SelectedReanimTrack.Name = ReanimTransformDialog.TrackName;
            }

            if (ReanimTransformDialog.Frame >= 0d)
            {
                NormalizeVisibleTransform(ref transform);
            }
            else
            {
                transform.mFrame = -1f;
            }

            transform.mTransX = (float)ReanimTransformDialog.X;
            transform.mTransY = (float)ReanimTransformDialog.Y;
            transform.mScaleX = (float)ReanimTransformDialog.ScaleX;
            transform.mScaleY = (float)ReanimTransformDialog.ScaleY;
            transform.mSkewX = (float)ReanimTransformDialog.SkewX;
            transform.mSkewY = (float)ReanimTransformDialog.SkewY;
            transform.mFrame = (float)ReanimTransformDialog.Frame;
            transform.mAlpha = (float)System.Math.Clamp(ReanimTransformDialog.Alpha, 0d, 1d);
            transform.mImage = string.IsNullOrWhiteSpace(ReanimTransformDialog.ImageId) ? null : ReanimTransformDialog.ImageId.Trim();
            transform.mFont = string.IsNullOrWhiteSpace(ReanimTransformDialog.FontId) ? null : ReanimTransformDialog.FontId.Trim();
            transform.mText = ReanimTransformDialog.Text ?? string.Empty;
            SetSelectedReanimTransform(transform);
            ApplyReanimTweensForSelection();
            SetSelectedReanimFrameResourceProperties(
                ReanimTransformDialog.Frame >= 0d,
                ReanimTransformDialog.ImageId,
                ReanimTransformDialog.FontId,
                ReanimTransformDialog.Text);
            RefreshReanimTimelineCells();
            OnPropertyChanged(nameof(CanTransformSelectedReanimFrame));
            OnPropertyChanged(nameof(CanDrag));
            ApplyReanimPropertyChanges();
        }

        private void CloseReanimTransformDialog()
        {
            IsReanimTransformDialogOpen = false;
        }

        private bool TryGetSelectedReanimTransform(out ReanimatorTransform transform)
        {
            transform = new ReanimatorTransform();
            if (_reanimDefinition?.mTracks is null || SelectedReanimTrack is null)
            {
                return false;
            }

            int trackIndex = SelectedReanimTrack.Index;
            int frameIndex = SelectedReanimFrameIndex;
            if (trackIndex < 0 ||
                trackIndex >= _reanimDefinition.mTrackCount ||
                frameIndex < 0 ||
                frameIndex >= _reanimDefinition.mTracks[trackIndex].mTransformCount)
            {
                return false;
            }

            transform = _reanimDefinition.mTracks[trackIndex].mTransforms[frameIndex];
            return true;
        }

        private void SetSelectedReanimTransform(ReanimatorTransform transform)
        {
            if (_reanimDefinition?.mTracks is null || SelectedReanimTrack is null)
            {
                return;
            }

            int trackIndex = SelectedReanimTrack.Index;
            int frameIndex = SelectedReanimFrameIndex;
            if (trackIndex < 0 ||
                trackIndex >= _reanimDefinition.mTrackCount ||
                frameIndex < 0 ||
                frameIndex >= _reanimDefinition.mTracks[trackIndex].mTransformCount)
            {
                return;
            }

            _reanimDefinition.mTracks[trackIndex].mTransforms[frameIndex] = transform;
        }

        private void RefreshSelectedFrameCell()
        {
            RaiseReanimTimelineChanged();
        }

        public void GetReanimTimelineFrameState(
            int trackIndex,
            int frameIndex,
            out bool hasContent,
            out bool hasImage,
            out bool isTweened)
        {
            hasContent = false;
            hasImage = false;
            isTweened = IsFrameTweened(trackIndex, frameIndex);
            if (_reanimDefinition?.mTracks is null ||
                trackIndex < 0 ||
                trackIndex >= _reanimDefinition.mTrackCount ||
                frameIndex < 0 ||
                frameIndex >= _reanimDefinition.mTracks[trackIndex].mTransformCount)
            {
                return;
            }

            ReanimatorTransform transform = _reanimDefinition.mTracks[trackIndex].mTransforms[frameIndex];
            hasContent = transform.mFrame >= 0f;
            hasImage = hasContent && !string.IsNullOrWhiteSpace(transform.mImage);
        }

        private void RefreshReanimTimelineCells()
        {
            RaiseReanimTimelineChanged();
            LoadSelectedReanimFrame();
        }

        private void UpdateSelectedReanimTrackState()
        {
            foreach (ReanimTrackViewModel track in ReanimTracks)
            {
                track.IsSelected = ReferenceEquals(track, SelectedReanimTrack);
            }

            OnPropertyChanged(nameof(CanRemoveReanimTrack));
            OnPropertyChanged(nameof(HasSelectedReanimFrame));
        }

        private void RebuildReanimTweenViewModels(int selectedIndex)
        {
            ReanimTweenViewModel previousSelection = SelectedReanimTween;
            _suppressReanimTweenSelection = true;
            ReanimTweens.Clear();

            if (_reanimAsset?.Tweens is not null)
            {
                int trackCount = System.Math.Max(1, ReanimTrackCount);
                int frameCount = System.Math.Max(3, ReanimFrameCount);
                for (int i = 0; i < _reanimAsset.Tweens.Count; i++)
                {
                    ReanimTweens.Add(new ReanimTweenViewModel(
                        i,
                        _reanimAsset.Tweens[i],
                        trackCount,
                        frameCount,
                        GetReanimTrackName,
                        OnReanimTweenChanged));
                }
            }

            if (ReanimTweens.Count == 0)
            {
                SelectedReanimTween = null;
            }
            else if (previousSelection is not null && ReanimTweens.FirstOrDefault(tween => ReferenceEquals(tween.Model, previousSelection.Model)) is ReanimTweenViewModel retainedTween)
            {
                SelectedReanimTween = retainedTween;
            }
            else if (selectedIndex >= 0)
            {
                SelectedReanimTween = ReanimTweens[System.Math.Clamp(selectedIndex, 0, ReanimTweens.Count - 1)];
            }
            else
            {
                SelectedReanimTween = null;
            }

            _suppressReanimTweenSelection = false;
            OnPropertyChanged(nameof(ReanimTweens));
            RaiseReanimTweenPropertiesChanged();
        }

        private void RefreshReanimTweenTrackNames()
        {
            foreach (ReanimTweenViewModel tween in ReanimTweens)
            {
                tween.RefreshTrackName();
            }
        }

        private void OnReanimTweenChanged(ReanimTweenViewModel tween)
        {
            NormalizeReanimTweenMetadata();
            ApplyReanimTween(tween?.Model);
            RefreshReanimTimelineCells();
            RaiseReanimTweenPropertiesChanged();
            ApplyReanimPropertyChanges();
        }

        private void RaiseReanimTweenPropertiesChanged()
        {
            OnPropertyChanged(nameof(ReanimTweenCount));
            OnPropertyChanged(nameof(HasReanimTweenMetadata));
            OnPropertyChanged(nameof(HasNoReanimTweenMetadata));
            OnPropertyChanged(nameof(HasSelectedReanimTween));
            OnPropertyChanged(nameof(IsSelectedReanimFrameTweened));
            OnPropertyChanged(nameof(CanTransformSelectedReanimFrame));
            OnPropertyChanged(nameof(CanDrag));
            OnPropertyChanged(nameof(CanMakeSelectedReanimFrameKeyframe));
            OnPropertyChanged(nameof(CanEditReanimTweens));
            OnPropertyChanged(nameof(SelectedReanimTweenSummary));
        }

        private void UpdateReanimTimelineSelection()
        {
            RaiseReanimTimelineChanged();
            OnPropertyChanged(nameof(CanRemoveReanimFrame));
            OnPropertyChanged(nameof(SelectedReanimFrameSummary));
            OnPropertyChanged(nameof(IsSelectedReanimFrameTweened));
            OnPropertyChanged(nameof(CanTransformSelectedReanimFrame));
            OnPropertyChanged(nameof(CanDrag));
            OnPropertyChanged(nameof(CanMakeSelectedReanimFrameKeyframe));
        }

        private void RaiseReanimTimelineChanged()
        {
            _reanimTimelineRevision++;
            OnPropertyChanged(nameof(ReanimTimelineRevision));
        }

        private void RaiseReanimStructureChanged()
        {
            OnPropertyChanged(nameof(ReanimTrackCount));
            OnPropertyChanged(nameof(ReanimFrameCount));
            OnPropertyChanged(nameof(ReanimDurationSummary));
            OnPropertyChanged(nameof(HasReanimControls));
            OnPropertyChanged(nameof(HasSelectedReanimFrame));
            OnPropertyChanged(nameof(IsSelectedReanimFrameTweened));
            OnPropertyChanged(nameof(CanTransformSelectedReanimFrame));
            OnPropertyChanged(nameof(CanDrag));
            OnPropertyChanged(nameof(CanRemoveReanimTrack));
            OnPropertyChanged(nameof(CanRemoveReanimFrame));
            OnPropertyChanged(nameof(ReanimTweenCount));
            OnPropertyChanged(nameof(HasReanimTweenMetadata));
            OnPropertyChanged(nameof(HasNoReanimTweenMetadata));
            OnPropertyChanged(nameof(CanEditReanimTweens));
            OnPropertyChanged(nameof(CanMakeSelectedReanimFrameKeyframe));
        }

        private bool IsFrameTweened(int trackIndex, int frameIndex)
        {
            if (_reanimAsset?.Tweens is null)
            {
                return false;
            }

            foreach (ReanimTween tween in _reanimAsset.Tweens)
            {
                if (tween.TrackIndex == trackIndex &&
                    frameIndex > tween.StartFrame &&
                    frameIndex < tween.EndFrame)
                {
                    return true;
                }
            }

            return false;
        }

        private void NormalizeReanimTweenMetadata()
        {
            if (_reanimAsset is null)
            {
                return;
            }

            _reanimAsset.Tweens ??= [];
            int trackCount = _reanimDefinition?.mTracks is null ? 0 : System.Math.Min(_reanimDefinition.mTrackCount, _reanimDefinition.mTracks.Length);
            int frameCount = ReanimFrameCount;
            for (int i = _reanimAsset.Tweens.Count - 1; i >= 0; i--)
            {
                ReanimTween tween = _reanimAsset.Tweens[i];
                if (trackCount <= 0 || frameCount <= 1)
                {
                    _reanimAsset.Tweens.RemoveAt(i);
                    continue;
                }

                tween.TrackIndex = System.Math.Clamp(tween.TrackIndex, 0, trackCount - 1);
                tween.TrackName = GetReanimTrackName(tween.TrackIndex);
                tween.StartFrame = System.Math.Clamp(tween.StartFrame, 0, frameCount - 1);
                tween.EndFrame = System.Math.Clamp(tween.EndFrame, 0, frameCount - 1);
                if (tween.EndFrame <= tween.StartFrame + 1)
                {
                    _reanimAsset.Tweens.RemoveAt(i);
                    continue;
                }

                tween.Properties = NormalizeTweenProperties(tween.Properties);
            }
        }

        private void InsertReanimTweenFrame(int frameIndex)
        {
            if (_reanimAsset?.Tweens is null)
            {
                return;
            }

            foreach (ReanimTween tween in _reanimAsset.Tweens)
            {
                if (tween.StartFrame >= frameIndex)
                {
                    tween.StartFrame++;
                }

                if (tween.EndFrame >= frameIndex)
                {
                    tween.EndFrame++;
                }
            }
        }

        private void RemoveReanimTweenFrame(int frameIndex)
        {
            if (_reanimAsset?.Tweens is null)
            {
                return;
            }

            foreach (ReanimTween tween in _reanimAsset.Tweens)
            {
                if (tween.StartFrame > frameIndex)
                {
                    tween.StartFrame--;
                }

                if (tween.EndFrame > frameIndex)
                {
                    tween.EndFrame--;
                }
                else if (tween.EndFrame == frameIndex)
                {
                    tween.EndFrame = System.Math.Max(0, tween.EndFrame - 1);
                }
            }
        }

        private void RemoveReanimTweenTrack(int trackIndex)
        {
            if (_reanimAsset?.Tweens is null)
            {
                return;
            }

            for (int i = _reanimAsset.Tweens.Count - 1; i >= 0; i--)
            {
                ReanimTween tween = _reanimAsset.Tweens[i];
                if (tween.TrackIndex == trackIndex)
                {
                    _reanimAsset.Tweens.RemoveAt(i);
                }
                else if (tween.TrackIndex > trackIndex)
                {
                    tween.TrackIndex--;
                }
            }
        }

        private void UpdateReanimTweenTrackName(int trackIndex, string trackName)
        {
            if (_reanimAsset?.Tweens is null)
            {
                return;
            }

            foreach (ReanimTween tween in _reanimAsset.Tweens)
            {
                if (tween.TrackIndex == trackIndex)
                {
                    tween.TrackName = trackName ?? string.Empty;
                }
            }
        }

        private void ApplyReanimTweensForSelection()
        {
            if (SelectedReanimTrack is null || _reanimAsset?.Tweens is null)
            {
                return;
            }

            int trackIndex = SelectedReanimTrack.Index;
            int frameIndex = SelectedReanimFrameIndex;
            foreach (ReanimTween tween in _reanimAsset.Tweens)
            {
                if (tween.TrackIndex == trackIndex &&
                    (tween.StartFrame == frameIndex || tween.EndFrame == frameIndex))
                {
                    ApplyReanimTween(tween);
                }
            }
        }

        private void ApplyAllReanimTweens()
        {
            if (_reanimAsset?.Tweens is null)
            {
                return;
            }

            foreach (ReanimTween tween in _reanimAsset.Tweens)
            {
                ApplyReanimTween(tween);
            }
        }

        private void ApplyReanimTween(ReanimTween tween)
        {
            if (_reanimDefinition?.mTracks is null ||
                tween is null ||
                tween.TrackIndex < 0 ||
                tween.TrackIndex >= _reanimDefinition.mTrackCount)
            {
                return;
            }

            ReanimatorTrack track = _reanimDefinition.mTracks[tween.TrackIndex];
            if (track?.mTransforms is null ||
                tween.StartFrame < 0 ||
                tween.EndFrame >= track.mTransformCount ||
                tween.EndFrame <= tween.StartFrame + 1)
            {
                return;
            }

            ReanimatorTransform start = track.mTransforms[tween.StartFrame];
            ReanimatorTransform end = track.mTransforms[tween.EndFrame];
            tween.Properties = NormalizeTweenProperties(tween.Properties);
            int span = tween.EndFrame - tween.StartFrame;
            for (int frameIndex = tween.StartFrame + 1; frameIndex < tween.EndFrame; frameIndex++)
            {
                float fraction = (frameIndex - tween.StartFrame) / (float)span;
                ReanimatorTransform transform = track.mTransforms[frameIndex];
                transform.mTransX = Lerp(start.mTransX, end.mTransX, fraction);
                transform.mTransY = Lerp(start.mTransY, end.mTransY, fraction);
                transform.mSkewX = Lerp(start.mSkewX, end.mSkewX, fraction);
                transform.mSkewY = Lerp(start.mSkewY, end.mSkewY, fraction);
                transform.mScaleX = Lerp(start.mScaleX, end.mScaleX, fraction);
                transform.mScaleY = Lerp(start.mScaleY, end.mScaleY, fraction);
                transform.mAlpha = Lerp(start.mAlpha, end.mAlpha, fraction);
                transform.mFrame = start.mFrame;
                transform.mImage = start.mImage;
                transform.mFont = start.mFont;
                transform.mText = start.mText;
                track.mTransforms[frameIndex] = transform;
            }
        }

        private void BakeReanimTween(
            ReanimTween tween,
            bool removeTween,
            bool refresh = true,
            bool markDirty = true)
        {
            if (tween is null)
            {
                return;
            }

            ApplyReanimTween(tween);
            if (removeTween && _reanimAsset?.Tweens is not null)
            {
                _reanimAsset.Tweens.Remove(tween);
            }

            if (!refresh)
            {
                return;
            }

            NormalizeReanimTweenMetadata();
            RebuildReanimTweenViewModels(System.Math.Min(SelectedReanimTween?.Index ?? 0, ReanimTweenCount - 1));
            RefreshReanimTimelineCells();
            RaiseReanimTweenPropertiesChanged();
            ApplyReanimPropertyChanges(markDirty);
        }

        private void AddSplitReanimTween(ReanimTween source, int startFrame, int endFrame)
        {
            if (_reanimAsset?.Tweens is null || endFrame <= startFrame + 1)
            {
                return;
            }

            _reanimAsset.Tweens.Add(new ReanimTween
            {
                TrackIndex = source.TrackIndex,
                TrackName = GetReanimTrackName(source.TrackIndex),
                StartFrame = startFrame,
                EndFrame = endFrame,
                Properties = ReanimTween.CreateTweenedProperties()
            });
        }

        private static List<ReanimTween> CloneReanimTweens(IEnumerable<ReanimTween> source)
        {
            if (source is null)
            {
                return [];
            }

            return source
                .Select(tween => new ReanimTween
                {
                    TrackIndex = tween.TrackIndex,
                    TrackName = tween.TrackName ?? string.Empty,
                    StartFrame = tween.StartFrame,
                    EndFrame = tween.EndFrame,
                    Properties = ReanimTween.CreateTweenedProperties()
                })
                .ToList();
        }

        private string GetReanimTrackName(int trackIndex)
        {
            if (_reanimDefinition?.mTracks is null ||
                trackIndex < 0 ||
                trackIndex >= _reanimDefinition.mTrackCount)
            {
                return string.Empty;
            }

            return _reanimDefinition.mTracks[trackIndex]?.mName ?? string.Empty;
        }

        private static List<string> NormalizeTweenProperties(IEnumerable<string> properties)
        {
            return ReanimTween.CreateTweenedProperties();
        }

        private static List<ReanimTween> InferReanimTweens(ReanimatorDefinition definition)
        {
            if (definition?.mTracks is null || definition.mTrackCount <= 0)
            {
                return [];
            }

            List<ReanimTween> tweens = [];
            int trackCount = System.Math.Min(definition.mTrackCount, definition.mTracks.Length);
            for (int trackIndex = 0; trackIndex < trackCount; trackIndex++)
            {
                ReanimatorTrack track = definition.mTracks[trackIndex];
                if (track?.mTransforms is null || track.mTransformCount < 3)
                {
                    continue;
                }

                AddInferredTweens(tweens, track, trackIndex);
            }

            return tweens
                .OrderBy(tween => tween.TrackIndex)
                .ThenBy(tween => tween.StartFrame)
                .ThenBy(tween => tween.EndFrame)
                .ToList();
        }

        private static void AddInferredTweens(
            List<ReanimTween> tweens,
            ReanimatorTrack track,
            int trackIndex)
        {
            int count = System.Math.Min(track.mTransformCount, track.mTransforms.Length);
            int start = 0;
            while (start < count - 2)
            {
                if (!CanInferTweenFromFrame(track.mTransforms[start]))
                {
                    start++;
                    continue;
                }

                int end = FindLinearTweenRunEnd(track.mTransforms, count, start);
                if (end > start + 1 &&
                    HasConstantTweenResourceFields(track.mTransforms, start, end))
                {
                    tweens.Add(new ReanimTween
                    {
                        TrackIndex = trackIndex,
                        TrackName = track.mName ?? string.Empty,
                        StartFrame = start,
                        EndFrame = end,
                        Properties = ReanimTween.CreateTweenedProperties()
                    });
                    start = end;
                }
                else
                {
                    start++;
                }
            }
        }

        private static int FindLinearTweenRunEnd(
            ReanimatorTransform[] transforms,
            int count,
            int start)
        {
            ReanimatorTransform first = transforms[start];

            int end = start + 1;
            for (int candidateEnd = start + 2; candidateEnd < count; candidateEnd++)
            {
                if (!CanInferTweenFromFrame(transforms[candidateEnd]))
                {
                    break;
                }

                if (!HasSameTweenResourceFields(first, transforms[candidateEnd]))
                {
                    break;
                }

                if (IsLinearTweenRun(transforms, start, candidateEnd))
                {
                    end = candidateEnd;
                }
            }

            return end;
        }

        private static bool CanInferTweenFromFrame(ReanimatorTransform transform)
        {
            return transform.mFrame >= 0f;
        }

        private static bool HasSameTweenResourceFields(ReanimatorTransform first, ReanimatorTransform second)
        {
            return NearlyEqual(second.mFrame, first.mFrame, 0.15f) &&
                string.Equals(second.mImage ?? string.Empty, first.mImage ?? string.Empty, System.StringComparison.Ordinal) &&
                string.Equals(second.mFont ?? string.Empty, first.mFont ?? string.Empty, System.StringComparison.Ordinal) &&
                string.Equals(second.mText ?? string.Empty, first.mText ?? string.Empty, System.StringComparison.Ordinal);
        }

        private static bool IsLinearTweenRun(
            ReanimatorTransform[] transforms,
            int startFrame,
            int endFrame)
        {
            ReanimatorTransform start = transforms[startFrame];
            ReanimatorTransform end = transforms[endFrame];
            int span = endFrame - startFrame;
            for (int frameIndex = startFrame + 1; frameIndex <= endFrame; frameIndex++)
            {
                ReanimatorTransform transform = transforms[frameIndex];
                if (!CanInferTweenFromFrame(transform))
                {
                    return false;
                }

                if (!HasSameTweenResourceFields(start, transform))
                {
                    return false;
                }

                if (frameIndex == endFrame)
                {
                    continue;
                }

                float fraction = (frameIndex - startFrame) / (float)span;
                if (!IsExpectedTweenTransform(start, end, transform, fraction))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsExpectedTweenTransform(
            ReanimatorTransform start,
            ReanimatorTransform end,
            ReanimatorTransform transform,
            float fraction)
        {
            return NearlyEqual(transform.mTransX, Lerp(start.mTransX, end.mTransX, fraction), 0.15f) &&
                NearlyEqual(transform.mTransY, Lerp(start.mTransY, end.mTransY, fraction), 0.15f) &&
                NearlyEqual(transform.mSkewX, Lerp(start.mSkewX, end.mSkewX, fraction), 0.15f) &&
                NearlyEqual(transform.mSkewY, Lerp(start.mSkewY, end.mSkewY, fraction), 0.15f) &&
                NearlyEqual(transform.mScaleX, Lerp(start.mScaleX, end.mScaleX, fraction), 0.0015f) &&
                NearlyEqual(transform.mScaleY, Lerp(start.mScaleY, end.mScaleY, fraction), 0.0015f) &&
                NearlyEqual(transform.mAlpha, Lerp(start.mAlpha, end.mAlpha, fraction), 0.0015f);
        }

        private static bool HasConstantTweenResourceFields(ReanimatorTransform[] transforms, int startFrame, int endFrame)
        {
            ReanimatorTransform start = transforms[startFrame];
            for (int frameIndex = startFrame + 1; frameIndex <= endFrame; frameIndex++)
            {
                ReanimatorTransform transform = transforms[frameIndex];
                if (!CanInferTweenFromFrame(transform))
                {
                    return false;
                }

                if (!HasSameTweenResourceFields(start, transform))
                {
                    return false;
                }
            }

            return true;
        }

        private static float Lerp(float start, float end, float fraction)
        {
            return start + ((end - start) * fraction);
        }

        private static bool NearlyEqual(float left, float right, float delta)
        {
            return System.Math.Abs(left - right) <= delta;
        }

        private static ReanimatorDefinition CreateEmptyReanimDefinition()
        {
            ReanimatorDefinition definition = new()
            {
                mFPS = 12f,
                mTrackCount = 1,
                mTracks =
                [
                    new ReanimatorTrack("track_1", 1)
                ]
            };
            definition.mTracks[0].mTransforms[0] = CreateDefaultReanimTransform(true);
            definition.Init();
            return definition;
        }

        private static void NormalizeReanimDefinition(ReanimatorDefinition definition)
        {
            if (definition is null)
            {
                return;
            }

            definition.mFPS = definition.mFPS <= 0f ? 12f : definition.mFPS;
            definition.mTracks ??= [];
            int trackCount = System.Math.Clamp((int)definition.mTrackCount, 0, definition.mTracks.Length);
            definition.mTrackCount = (short)trackCount;
            if (trackCount == 0)
            {
                definition.mTracks =
                [
                    new ReanimatorTrack("track_1", 1)
                ];
                definition.mTracks[0].mTransforms[0] = CreateDefaultReanimTransform(true);
                definition.mTrackCount = 1;
                trackCount = 1;
            }

            int frameCount = 1;
            for (int i = 0; i < trackCount; i++)
            {
                ReanimatorTrack track = definition.mTracks[i] ?? new ReanimatorTrack($"track_{i + 1}", 1);
                definition.mTracks[i] = track;
                track.mName ??= string.Empty;
                track.mTransforms ??= [];
                int transformCount = System.Math.Clamp((int)track.mTransformCount, 0, track.mTransforms.Length);
                frameCount = System.Math.Max(frameCount, transformCount);
            }

            for (int i = 0; i < trackCount; i++)
            {
                NormalizeReanimTrack(definition.mTracks[i], frameCount);
            }

            definition.Init();
        }

        private static void NormalizeReanimTrack(ReanimatorTrack track, int frameCount)
        {
            ReanimatorTransform[] transforms = track.mTransforms ?? [];
            int oldLength = transforms.Length;
            System.Array.Resize(ref transforms, frameCount);
            for (int i = oldLength; i < transforms.Length; i++)
            {
                transforms[i] = CreateDefaultReanimTransform(false);
            }

            ReanimatorTransform previous = CreateDefaultReanimTransform(true);
            string previousImage = null;
            string previousFont = null;
            string previousText = string.Empty;

            for (int i = 0; i < frameCount; i++)
            {
                ReanimatorTransform transform = transforms[i];
                if (IsPlaceholder(transform.mTransX)) transform.mTransX = previous.mTransX;
                if (IsPlaceholder(transform.mTransY)) transform.mTransY = previous.mTransY;
                if (IsPlaceholder(transform.mSkewX)) transform.mSkewX = previous.mSkewX;
                if (IsPlaceholder(transform.mSkewY)) transform.mSkewY = previous.mSkewY;
                if (IsPlaceholder(transform.mScaleX)) transform.mScaleX = previous.mScaleX;
                if (IsPlaceholder(transform.mScaleY)) transform.mScaleY = previous.mScaleY;
                if (IsPlaceholder(transform.mFrame)) transform.mFrame = previous.mFrame;
                if (IsPlaceholder(transform.mAlpha)) transform.mAlpha = previous.mAlpha;

                if (transform.mImage is null)
                {
                    transform.mImage = previousImage;
                }
                else
                {
                    previousImage = transform.mImage;
                }

                if (transform.mFont is null)
                {
                    transform.mFont = previousFont;
                }
                else
                {
                    previousFont = transform.mFont;
                }

                if (string.IsNullOrEmpty(transform.mText))
                {
                    transform.mText = previousText;
                }
                else
                {
                    previousText = transform.mText;
                }

                transforms[i] = transform;
                previous = transform;
            }

            track.mTransforms = transforms;
            track.mTransformCount = (short)transforms.Length;
        }

        private static ReanimatorTransform CreateDefaultReanimTransform(bool visible)
        {
            return new ReanimatorTransform
            {
                mTransX = 0f,
                mTransY = 0f,
                mSkewX = 0f,
                mSkewY = 0f,
                mScaleX = 1f,
                mScaleY = 1f,
                mFrame = visible ? 0f : -1f,
                mAlpha = 1f,
                mImage = null,
                mFont = null,
                mText = string.Empty
            };
        }

        private static void NormalizeVisibleTransform(ref ReanimatorTransform transform)
        {
            if (IsPlaceholder(transform.mTransX)) transform.mTransX = 0f;
            if (IsPlaceholder(transform.mTransY)) transform.mTransY = 0f;
            if (IsPlaceholder(transform.mSkewX)) transform.mSkewX = 0f;
            if (IsPlaceholder(transform.mSkewY)) transform.mSkewY = 0f;
            if (IsPlaceholder(transform.mScaleX)) transform.mScaleX = 1f;
            if (IsPlaceholder(transform.mScaleY)) transform.mScaleY = 1f;
            if (IsPlaceholder(transform.mFrame) || transform.mFrame < 0f) transform.mFrame = 0f;
            if (IsPlaceholder(transform.mAlpha)) transform.mAlpha = 1f;
            transform.mText ??= string.Empty;
        }

        private static void InsertTransform(ReanimatorTrack track, int index, ReanimatorTransform transform)
        {
            ReanimatorTransform[] transforms = track.mTransforms ?? [];
            int insertIndex = System.Math.Clamp(index, 0, transforms.Length);
            System.Array.Resize(ref transforms, transforms.Length + 1);
            for (int i = transforms.Length - 1; i > insertIndex; i--)
            {
                transforms[i] = transforms[i - 1];
            }

            transforms[insertIndex] = transform;
            track.mTransforms = transforms;
            track.mTransformCount = (short)transforms.Length;
        }

        private static void RemoveTransform(ReanimatorTrack track, int index)
        {
            if (track?.mTransforms is null || track.mTransforms.Length <= 1)
            {
                return;
            }

            int removeIndex = System.Math.Clamp(index, 0, track.mTransforms.Length - 1);
            ReanimatorTransform[] transforms = new ReanimatorTransform[track.mTransforms.Length - 1];
            int target = 0;
            for (int i = 0; i < track.mTransforms.Length; i++)
            {
                if (i != removeIndex)
                {
                    transforms[target++] = track.mTransforms[i];
                }
            }

            track.mTransforms = transforms;
            track.mTransformCount = (short)transforms.Length;
        }

        private static ReanimatorDefinition CloneReanimDefinition(ReanimatorDefinition source)
        {
            if (source is null)
            {
                return null;
            }

            ReanimatorDefinition clone = new()
            {
                mFPS = source.mFPS,
                mTrackCount = source.mTrackCount,
                mTracks = new ReanimatorTrack[source.mTracks?.Length ?? 0]
            };

            if (source.mTracks is not null)
            {
                for (int i = 0; i < source.mTracks.Length; i++)
                {
                    ReanimatorTrack sourceTrack = source.mTracks[i];
                    if (sourceTrack is null)
                    {
                        continue;
                    }

                    ReanimatorTrack track = new(sourceTrack.mName ?? string.Empty, sourceTrack.mTransformCount);
                    int count = System.Math.Min(sourceTrack.mTransformCount, sourceTrack.mTransforms?.Length ?? 0);
                    track.mTransforms = new ReanimatorTransform[count];
                    track.mTransformCount = (short)count;
                    for (int frameIndex = 0; frameIndex < count; frameIndex++)
                    {
                        track.mTransforms[frameIndex] = sourceTrack.mTransforms[frameIndex];
                    }

                    clone.mTracks[i] = track;
                }
            }

            NormalizeReanimDefinition(clone);
            return clone;
        }

        private static bool IsPlaceholder(float value)
        {
            return value == ReanimatorXnaHelpers.DEFAULT_FIELD_PLACEHOLDER;
        }

        private static double RoundToThreeDecimals(double value)
        {
            double rounded = System.Math.Round(value, 3, System.MidpointRounding.AwayFromZero);
            return rounded == -0d ? 0d : rounded;
        }

        private void LoadParticleDefinition(TodParticleDefinition definition, bool markDirty)
        {
            _suppressParticlePropertyChanges = true;
            _particleDefinition = definition ?? ParticleDefinitionUtility.CreateEmpty();
            if (_particleDefinition.mEmitterDefs is null)
            {
                _particleDefinition.mEmitterDefs = [];
                _particleDefinition.mEmitterDefCount = 0;
            }

            int count = System.Math.Min(_particleDefinition.mEmitterDefCount, _particleDefinition.mEmitterDefs.Length);
            _particleDefinition.mEmitterDefCount = count;
            RebuildParticleEmitterViewModels(0);
            _suppressParticlePropertyChanges = false;
            ParticleDefinitionError = string.Empty;
            OnPropertyChanged(nameof(HasParticleDefinitionError));
            OnPropertyChanged(nameof(HasParticleControls));

            if (markDirty)
            {
                ApplyParticlePropertyChanges(markDirty: true);
            }
            else
            {
                RefreshParticlePreview();
            }
        }

        private void RebuildParticleEmitterViewModels(int selectedIndex)
        {
            foreach (ParticleEmitterViewModel emitter in ParticleEmitters)
            {
                emitter.Changed -= OnParticleEmitterChanged;
                emitter.Dispose();
            }

            ParticleEmitters.Clear();
            if (_particleDefinition?.mEmitterDefs is null)
            {
                SelectedParticleEmitter = null;
                return;
            }

            int count = System.Math.Min(_particleDefinition.mEmitterDefCount, _particleDefinition.mEmitterDefs.Length);
            _particleDefinition.mEmitterDefCount = count;
            for (int i = 0; i < count; i++)
            {
                ParticleEmitterViewModel emitter = new(i, _particleDefinition.mEmitterDefs[i]);
                emitter.Changed += OnParticleEmitterChanged;
                ParticleEmitters.Add(emitter);
            }

            SelectedParticleEmitter = ParticleEmitters.Count == 0
                ? null
                : ParticleEmitters[System.Math.Clamp(selectedIndex, 0, ParticleEmitters.Count - 1)];
            OnPropertyChanged(nameof(CanRemoveParticleEmitter));
            OnPropertyChanged(nameof(ParticleEmitters));
        }

        private static TodEmitterDefinition CreateDefaultEmitter(int index)
        {
            return new TodEmitterDefinition
            {
                mName = $"Emitter {index + 1}"
            };
        }

        private void SetTrailProperties(
            string imageId,
            int maxPoints,
            float minPointDistance,
            bool loops,
            FloatParameterTrack widthOverLength,
            FloatParameterTrack alphaOverLength,
            FloatParameterTrack widthOverTime,
            FloatParameterTrack alphaOverTime,
            FloatParameterTrack duration,
            bool markDirty)
        {
            _suppressTrailPropertyChanges = true;
            TrailImageId = imageId ?? string.Empty;
            TrailMaxPoints = maxPoints;
            TrailMinPointDistance = minPointDistance;
            TrailLoops = loops;
            TrailWidthOverLength.LoadFrom(widthOverLength);
            TrailAlphaOverLength.LoadFrom(alphaOverLength);
            TrailWidthOverTime.LoadFrom(widthOverTime);
            TrailAlphaOverTime.LoadFrom(alphaOverTime);
            TrailDuration.LoadFrom(duration);
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

        private void ApplyParticlePropertyChanges(bool markDirty = true)
        {
            if (_particleDefinition is null || _suppressParticlePropertyChanges)
            {
                return;
            }

            if (!TryApplyParticleEmitters())
            {
                if (markDirty)
                {
                    MarkDirty();
                }

                return;
            }

            RefreshParticlePreview();

            if (markDirty)
            {
                MarkDirty();
            }
        }

        private void RefreshParticlePreview()
        {
            if (Kind != EffectAssetKind.Particle || _particleDefinition is null)
            {
                return;
            }

            if (PreviewFrameProvider is System.IDisposable disposableProvider)
            {
                disposableProvider.Dispose();
            }

            PreviewFrameProvider = new ParticlePreviewSimulation(_project, _particleDefinition, AssetId);
            PreviewFrame = EffectPreviewFrameBuilder.BuildPlaceholder(Kind, AssetId);
        }

        private bool TryApplyParticleEmitters()
        {
            if (_particleDefinition?.mEmitterDefs is null)
            {
                return true;
            }

            try
            {
                int count = System.Math.Min(_particleDefinition.mEmitterDefCount, _particleDefinition.mEmitterDefs.Length);
                foreach (ParticleEmitterViewModel emitter in ParticleEmitters)
                {
                    if (emitter.Index >= 0 && emitter.Index < count)
                    {
                        emitter.ApplyTo(_particleDefinition.mEmitterDefs[emitter.Index]);
                    }
                }
            }
            catch (System.Exception ex) when (ex is System.FormatException or System.OverflowException)
            {
                ParticleDefinitionError = ex.Message;
                OnPropertyChanged(nameof(HasParticleDefinitionError));
                return false;
            }

            ParticleDefinitionError = string.Empty;
            OnPropertyChanged(nameof(HasParticleDefinitionError));
            return true;
        }

        private bool TryApplyTrailTracks()
        {
            try
            {
                TrailWidthOverLength.ApplyTo(_trailDefinition.mWidthOverLength);
                TrailAlphaOverLength.ApplyTo(_trailDefinition.mAlphaOverLength);
                TrailWidthOverTime.ApplyTo(_trailDefinition.mWidthOverTime);
                TrailAlphaOverTime.ApplyTo(_trailDefinition.mAlphaOverTime);
                TrailDuration.ApplyTo(_trailDefinition.mTrailDuration);
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

        private static string ResolveEffectPath(EffectProject project, string path, EffectAsset asset)
        {
            string assetPath = asset?.Path;
            string effectivePath = string.IsNullOrWhiteSpace(assetPath) ? path : assetPath;
            return TrailPreviewFrameBuilder.ResolvePath(project, effectivePath);
        }

        public override void Dispose()
        {
            TrailWidthOverLength.Changed -= OnTrailTrackChanged;
            TrailAlphaOverLength.Changed -= OnTrailTrackChanged;
            TrailWidthOverTime.Changed -= OnTrailTrackChanged;
            TrailAlphaOverTime.Changed -= OnTrailTrackChanged;
            TrailDuration.Changed -= OnTrailTrackChanged;
            foreach (ParticleEmitterViewModel emitter in ParticleEmitters)
            {
                emitter.Changed -= OnParticleEmitterChanged;
                emitter.Dispose();
            }

            base.Dispose();
        }
    }
}
