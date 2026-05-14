using System.Collections.ObjectModel;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EffectViewer.Assets;
using EffectViewer.Localization;
using EffectViewer.Projects;
using EffectViewer.Rendering;
using EffectViewer.Rendering.Export;
using EffectViewer.Runtime;
using EffectViewer.EffectRuntime.Common;
using EffectViewer.EffectRuntime.Graphics;
using EffectViewer.EffectRuntime.Particle;
using EffectViewer.EffectRuntime.Reanim;
using EffectViewer.EffectRuntime.Trail;

namespace EffectViewer.ViewModels
{
    public sealed partial class EffectEditorViewModel : EditorViewModelBase, IViewportDragHandler
    {
        private readonly EffectProject _project;
        private readonly ReanimPreviewSimulation _reanimPreview;
        private readonly ReanimAsset _reanimAsset;
        private readonly Dictionary<string, Image> _imageSizeCache = new(StringComparer.OrdinalIgnoreCase);
        private const int MaxUndoHistoryCount = 100;
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
        private ParticleDefinition _particleDefinition;
        private ParticleDefinition _savedParticleDefinition;
        private string _selectedReanimLayer;
        private ReanimTrackViewModel _selectedReanimTrack;
        private int _selectedReanimFrameIndex;
        private double _reanimFps = 12d;
        private bool _reanimIsPlaying;
        private bool _isViewportFreeTransformEnabled;
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
        private bool _syncingReanimPlaybackFrame;
        private bool _suppressTrailPropertyChanges;
        private bool _suppressParticlePropertyChanges;
        private int _reanimTimelineRevision;
        private ViewportDragHandle _activeViewportDragHandle;
        private Vector2 _viewportDragStartWorld;
        private ReanimatorTransform _viewportDragStartTransform;
        private Vector2 _viewportDragStartSize;
        private readonly List<EffectEditorHistorySnapshot> _undoStack = [];
        private readonly List<EffectEditorHistorySnapshot> _redoStack = [];
        private EffectEditorHistorySnapshot _undoBatchSnapshot;
        private bool _undoBatchRecorded;
        private bool _isRestoringHistory;
        private bool _suppressUndoRecording;

        [ObservableProperty]
        private string _assetId;

        private string _savedAssetId;
        public string Path { get; }
        public string EditorSummary => Kind switch
        {
            EffectAssetKind.Reanim => T("EffectEditor.EditorSummaryReanim"),
            EffectAssetKind.Particle => T("EffectEditor.EditorSummaryParticle"),
            EffectAssetKind.Trail => T("EffectEditor.EditorSummaryTrail"),
            _ => T("EffectEditor.EditorSummary")
        };
        public EffectFileSummary FileSummary { get; }
        public ObservableCollection<string> ReanimLayers { get; } = [];
        public ObservableCollection<ReanimTrackViewModel> ReanimTracks { get; } = [];
        public ObservableCollection<ReanimTweenViewModel> ReanimTweens { get; } = [];
        public int ReanimTimelineRevision => _reanimTimelineRevision;
        public ObservableCollection<ParticleEmitterViewModel> ParticleEmitters { get; } = [];
        public override bool CanUndo => _undoStack.Count > 0;
        public override bool CanRedo => _redoStack.Count > 0;
        public bool IsReanimEditor => Kind == EffectAssetKind.Reanim;
        public bool IsParticleEditor => Kind == EffectAssetKind.Particle;
        public bool IsTrailEditor => Kind == EffectAssetKind.Trail;
        public bool HasReanimControls => Kind == EffectAssetKind.Reanim && ReanimTracks.Count > 0;
        public bool HasSelectedReanimFrame => IsReanimEditor && SelectedReanimTrack is not null && ReanimFrameCount > 0;
        public bool IsSelectedReanimFrameTweened => SelectedReanimTrack is not null &&
            IsFrameTweened(SelectedReanimTrack.Index, SelectedReanimFrameIndex);
        public bool CanTransformSelectedReanimFrame => HasSelectedReanimFrame && !IsSelectedReanimFrameTweened;
        public bool CanDrag => IsViewportFreeTransformEnabled && CanTransformSelectedReanimFrame && SelectedReanimFrameVisible;
        public bool IsPanModeEnabled => !IsViewportFreeTransformEnabled;
        public ViewportTransformBox? TransformBox => TryGetSelectedTransformBox(out ViewportTransformBox box) ? box : null;
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
            ? T("EffectEditor.ZeroSeconds")
            : F("EffectEditor.DurationSeconds", ReanimFrameCount / ReanimFps);
        public string SelectedReanimFrameSummary => HasSelectedReanimFrame
            ? F("EffectEditor.SelectedFrameSummary", SelectedReanimTrack.DisplayName, SelectedReanimFrameIndex + 1)
            : T("EffectEditor.NoFrameSelected");
        public ReanimTransformDialogViewModel ReanimTransformDialog { get; }
        private ReanimTweenViewModel _selectedReanimTween;
        public ReanimTweenViewModel SelectedReanimTween
        {
            get => _selectedReanimTween;
            set
            {
                if (SetProperty(ref _selectedReanimTween, value))
                {
                    OnPropertyChanged(nameof(HasSelectedReanimTween));
                    OnPropertyChanged(nameof(CanMakeSelectedReanimFrameKeyframe));
                    OnPropertyChanged(nameof(SelectedReanimTweenSummary));
                }
            }
        }

        public string SelectedReanimTweenSummary => SelectedReanimTween is null
            ? T("EffectEditor.NoTweenAtSelectedFrame")
            : F("EffectEditor.SelectedTweenSummary", SelectedReanimTween.TrackName, SelectedReanimTween.StartFrameNumber, SelectedReanimTween.EndFrameNumber);
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
                int clamped = System.Math.Clamp(value, 2, EffectConstants.MAX_TRAIL_POINTS);
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

        public bool IsViewportFreeTransformEnabled
        {
            get => _isViewportFreeTransformEnabled;
            set
            {
                if (SetProperty(ref _isViewportFreeTransformEnabled, value))
                {
                    if (!value)
                    {
                        EndDrag();
                    }

                    OnPropertyChanged(nameof(IsPanModeEnabled));
                    OnPropertyChanged(nameof(CanDrag));
                    OnPropertyChanged(nameof(TransformBox));
                }
            }
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
                    SelectReanimTweenForCurrentCell();
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
                    if (!_syncingReanimPlaybackFrame)
                    {
                        ApplyReanimPreviewState();
                    }

                    SelectReanimTweenForCurrentCell();
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
        public override int PreviewExportDefaultFps => Kind == EffectAssetKind.Reanim
            ? System.Math.Clamp((int)System.Math.Round(ReanimFps), 1, 240)
            : base.PreviewExportDefaultFps;

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
                ParticleDefinition definition = _particleDefinition ?? LoadParticleDefinitionFromFile();
                if (_particleDefinition is not null && !TryApplyParticleEmitters())
                {
                    throw new InvalidDataException(ParticleDefinitionError);
                }

                ParticleDefinitionCodec.Encode(outputStream, definition, targetFileName);
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
            ? T("EffectEditor.NoImageReferences")
            : string.Join(", ", FileSummary.ImageIds);
        public string ResolvedImageReferenceSummary => FileSummary.ImageResolutions.Count == 0
            ? T("EffectEditor.NoResolvedImageReferences")
            : string.Join(", ", FileSummary.ImageResolutions);
        public string MissingImageSummary => FileSummary.MissingImageIds.Count == 0
            ? T("EffectEditor.NoMissingImageReferences")
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
            Loc.LanguageChanged += OnLanguageChanged;
            TrailWidthOverLength.Changed += OnTrailTrackChanged;
            TrailAlphaOverLength.Changed += OnTrailTrackChanged;
            TrailWidthOverTime.Changed += OnTrailTrackChanged;
            TrailAlphaOverTime.Changed += OnTrailTrackChanged;
            TrailDuration.Changed += OnTrailTrackChanged;
            if (kind == EffectAssetKind.Reanim)
            {
                _project.Assets.Reanims.TryGetValue(assetId, out _reanimAsset);
                _reanimPreview = new ReanimPreviewSimulation(project, path);
                _reanimPreview.CurrentFrameIndexChanged += OnReanimPreviewCurrentFrameIndexChanged;
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

        public override IRenderFrameProvider CreatePreviewExportFrameProvider()
        {
            IReadOnlyList<PreviewExportTimelineOption> timelines = GetPreviewExportTimelineOptions();
            string defaultTimelineId = GetDefaultPreviewExportTimelineId();
            PreviewExportTimelineOption timeline = timelines.FirstOrDefault(option =>
                string.Equals(option.Id, defaultTimelineId, System.StringComparison.OrdinalIgnoreCase)) ?? timelines.FirstOrDefault();
            return CreatePreviewExportFrameProvider(timeline);
        }

        public override IReadOnlyList<PreviewExportTimelineOption> GetPreviewExportTimelineOptions()
        {
            if (Kind != EffectAssetKind.Reanim || _reanimDefinition?.mTracks is null || _reanimDefinition.mTrackCount <= 0)
            {
                return [];
            }

            double sourceFps = ReanimFps <= 0d ? 12d : ReanimFps;
            List<PreviewExportTimelineOption> options =
            [
                new PreviewExportTimelineOption(
                    PreviewExportTimelineOption.FullTimelineId,
                    T("EffectEditor.FullTimeline"),
                    isFullTimeline: true,
                    frameStart: 0,
                    frameCount: GetFullReanimTimelineFrameCount(),
                    sourceFps)
            ];

            foreach (string layerName in BuildReanimLayerNames())
            {
                if (!TryGetReanimLayerFrameRange(layerName, out int frameStart, out int frameCount))
                {
                    continue;
                }

                options.Add(new PreviewExportTimelineOption(
                    layerName,
                    layerName,
                    isFullTimeline: false,
                    frameStart,
                    frameCount,
                    sourceFps));
            }

            return options;
        }

        public override string GetDefaultPreviewExportTimelineId()
        {
            if (Kind != EffectAssetKind.Reanim)
            {
                return null;
            }

            return PreviewExportTimelineOption.FullTimelineId;
        }

        public override IRenderFrameProvider CreatePreviewExportFrameProvider(PreviewExportTimelineOption timeline)
        {
            return Kind switch
            {
                EffectAssetKind.Reanim when _reanimDefinition is not null => CreateReanimExportProvider(),
                EffectAssetKind.Particle when _particleDefinition is not null => new ParticlePreviewSimulation(_project, _particleDefinition, AssetId)
                {
                    MaxUpdateStepsPerFrame = EffectConstants.TICKS_PER_SECOND * 2,
                    RestartAfterTicks = EffectConstants.TICKS_PER_SECOND * (int)PreviewExportOptions.MaximumTimeSeconds,
                    RestartOnComplete = false
                },
                EffectAssetKind.Trail when _trailDefinition is not null => new TrailPreviewSimulation(_trailDefinition, AssetId)
                {
                    MaxUpdateStepsPerFrame = EffectConstants.TICKS_PER_SECOND * 2,
                    RestartAfterTicks = EffectConstants.TICKS_PER_SECOND * (int)PreviewExportOptions.MaximumTimeSeconds,
                    RestartOnComplete = false
                },
                _ => null
            };
        }

        private IRenderFrameProvider CreateReanimExportProvider()
        {
            ReanimPreviewSimulation simulation = new(_project, Path)
            {
                MaxUpdateStepsPerFrame = EffectConstants.TICKS_PER_SECOND * 2
            };
            simulation.SetDefinition(_reanimDefinition);
            simulation.SetAnimRate((float)ReanimFps);
            simulation.SetLayer(null);
            simulation.SetPaused(false);
            foreach (ReanimTrackViewModel track in ReanimTracks)
            {
                simulation.SetTrackVisible(track.Index, track.IsVisible);
            }

            return simulation;
        }

        private static LocalizationManager Loc => LocalizationManager.Instance;
        private static string T(string key) => Loc.Text(key);
        private static string F(string key, params object[] args) => Loc.Format(key, args);

        private void OnLanguageChanged(object sender, EventArgs e)
        {
            OnPropertyChanged(nameof(EditorSummary));
            OnPropertyChanged(nameof(ReanimDurationSummary));
            OnPropertyChanged(nameof(SelectedReanimFrameSummary));
            OnPropertyChanged(nameof(SelectedReanimTweenSummary));
            OnPropertyChanged(nameof(ImageReferenceSummary));
            OnPropertyChanged(nameof(ResolvedImageReferenceSummary));
            OnPropertyChanged(nameof(MissingImageSummary));

            if (!IsReanimEditor)
            {
                return;
            }

            foreach (ReanimTrackViewModel track in ReanimTracks)
            {
                track.RefreshLocalizedText();
            }

            RefreshReanimLayers();
            ApplyReanimPreviewState();
        }

        partial void OnAssetIdChanged(string value)
        {
            if (_isRestoringHistory)
            {
                return;
            }

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

            RecordUndoSnapshot();
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

        public override void Undo()
        {
            if (!CanUndo)
            {
                return;
            }

            EndUndoBatch();
            EffectEditorHistorySnapshot current = CreateHistorySnapshot();
            EffectEditorHistorySnapshot previous = PopHistorySnapshot(_undoStack);
            if (current is not null)
            {
                PushHistorySnapshot(_redoStack, current);
            }

            RestoreHistorySnapshot(previous);
            MarkDirty();
            RaiseUndoRedoStateChanged();
        }

        public override void Redo()
        {
            if (!CanRedo)
            {
                return;
            }

            EndUndoBatch();
            EffectEditorHistorySnapshot current = CreateHistorySnapshot();
            EffectEditorHistorySnapshot next = PopHistorySnapshot(_redoStack);
            if (current is not null)
            {
                PushHistorySnapshot(_undoStack, current);
            }

            RestoreHistorySnapshot(next);
            MarkDirty();
            RaiseUndoRedoStateChanged();
        }

        private void RecordUndoSnapshot(EffectEditorHistorySnapshot snapshot = null)
        {
            if (_isRestoringHistory || _suppressUndoRecording)
            {
                return;
            }

            if (_undoBatchSnapshot is not null)
            {
                if (_undoBatchRecorded)
                {
                    return;
                }

                snapshot = _undoBatchSnapshot;
                _undoBatchRecorded = true;
            }
            else
            {
                snapshot ??= CreateHistorySnapshot();
            }

            if (snapshot is null)
            {
                return;
            }

            PushHistorySnapshot(_undoStack, snapshot);
            _redoStack.Clear();
            RaiseUndoRedoStateChanged();
        }

        private void BeginUndoBatch()
        {
            if (_isRestoringHistory || _undoBatchSnapshot is not null)
            {
                return;
            }

            _undoBatchSnapshot = CreateHistorySnapshot();
            _undoBatchRecorded = false;
        }

        private void EndUndoBatch()
        {
            _undoBatchSnapshot = null;
            _undoBatchRecorded = false;
        }

        private void ClearUndoRedoHistory()
        {
            EndUndoBatch();
            _undoStack.Clear();
            _redoStack.Clear();
            RaiseUndoRedoStateChanged();
        }

        private void RaiseUndoRedoStateChanged()
        {
            OnPropertyChanged(nameof(CanUndo));
            OnPropertyChanged(nameof(CanRedo));
        }

        private EffectEditorHistorySnapshot CreateHistorySnapshot()
        {
            return Kind switch
            {
                EffectAssetKind.Reanim when _reanimDefinition is not null => new EffectEditorHistorySnapshot
                {
                    Kind = EffectAssetKind.Reanim,
                    AssetId = GetCurrentAssetId(),
                    ReanimDefinition = CloneReanimDefinition(_reanimDefinition),
                    ReanimTweens = CloneReanimTweens(_reanimAsset?.Tweens),
                    SelectedReanimTrackIndex = SelectedReanimTrack?.Index ?? 0,
                    SelectedReanimFrameIndex = SelectedReanimFrameIndex
                },
                EffectAssetKind.Particle when _particleDefinition is not null => new EffectEditorHistorySnapshot
                {
                    Kind = EffectAssetKind.Particle,
                    AssetId = GetCurrentAssetId(),
                    ParticleDefinition = ParticleDefinitionUtility.Clone(_particleDefinition),
                    SelectedParticleEmitterIndex = SelectedParticleEmitter?.Index ?? 0
                },
                EffectAssetKind.Trail when _trailDefinition is not null => new EffectEditorHistorySnapshot
                {
                    Kind = EffectAssetKind.Trail,
                    AssetId = GetCurrentAssetId(),
                    TrailDefinition = CloneTrailDefinition(_trailDefinition)
                },
                _ => null
            };
        }

        private void RestoreHistorySnapshot(EffectEditorHistorySnapshot snapshot)
        {
            if (snapshot is null)
            {
                return;
            }

            try
            {
                _isRestoringHistory = true;
                RestoreHistoryAssetId(snapshot.AssetId);
                switch (snapshot.Kind)
                {
                    case EffectAssetKind.Reanim:
                        RestoreReanimHistorySnapshot(snapshot);
                        break;
                    case EffectAssetKind.Particle:
                        RestoreParticleHistorySnapshot(snapshot);
                        break;
                    case EffectAssetKind.Trail:
                        RestoreTrailHistorySnapshot(snapshot);
                        break;
                }
            }
            finally
            {
                _isRestoringHistory = false;
            }
        }

        private string GetCurrentAssetId()
        {
            return GetManifestAsset()?.Id ?? AssetId;
        }

        private void RestoreHistoryAssetId(string assetId)
        {
            string normalizedId = string.IsNullOrWhiteSpace(assetId)
                ? _savedAssetId
                : assetId;
            EffectAsset asset = GetManifestAsset();
            if (asset is not null)
            {
                asset.Id = normalizedId;
            }

            AssetId = normalizedId;
            Title = normalizedId;
            DocumentId = CreateDocumentId(Kind, normalizedId);
            _project?.RebuildAssetIndex();
            RefreshPreviewForAssetId();
            OnPropertyChanged(nameof(AssetId));
        }

        private void RestoreReanimHistorySnapshot(EffectEditorHistorySnapshot snapshot)
        {
            _reanimDefinition = CloneReanimDefinition(snapshot.ReanimDefinition) ?? CreateEmptyReanimDefinition();
            NormalizeReanimDefinition(_reanimDefinition);
            if (_reanimAsset is not null)
            {
                _reanimAsset.Tweens = CloneReanimTweens(snapshot.ReanimTweens);
                NormalizeReanimTweenMetadata();
            }

            RebuildReanimTweenViewModels();
            _suppressReanimPropertyChanges = true;
            ReanimFps = _reanimDefinition.mFPS <= 0f ? 12d : _reanimDefinition.mFPS;
            _suppressReanimPropertyChanges = false;
            RebuildReanimViewModels(snapshot.SelectedReanimTrackIndex, snapshot.SelectedReanimFrameIndex);
            ApplyReanimPreviewState();
            RaiseReanimTweenPropertiesChanged();
        }

        private void RestoreParticleHistorySnapshot(EffectEditorHistorySnapshot snapshot)
        {
            LoadParticleDefinition(ParticleDefinitionUtility.Clone(snapshot.ParticleDefinition), markDirty: false);
            if (ParticleEmitters.Count > 0)
            {
                SelectedParticleEmitter = ParticleEmitters[
                    System.Math.Clamp(snapshot.SelectedParticleEmitterIndex, 0, ParticleEmitters.Count - 1)];
            }
        }

        private void RestoreTrailHistorySnapshot(EffectEditorHistorySnapshot snapshot)
        {
            _trailDefinition = CloneTrailDefinition(snapshot.TrailDefinition) ?? new TrailDefinition();
            SetTrailProperties(
                _trailDefinition.mImage ?? string.Empty,
                _trailDefinition.mMaxPoints,
                _trailDefinition.mMinPointDistance,
                IsTrailFlagSet(_trailDefinition, TrailFlags.Loops),
                _trailDefinition.mWidthOverLength,
                _trailDefinition.mAlphaOverLength,
                _trailDefinition.mWidthOverTime,
                _trailDefinition.mAlphaOverTime,
                _trailDefinition.mTrailDuration,
                markDirty: false);
            RefreshTrailPreview();
        }

        private static void PushHistorySnapshot(List<EffectEditorHistorySnapshot> stack, EffectEditorHistorySnapshot snapshot)
        {
            if (snapshot is null)
            {
                return;
            }

            stack.Add(snapshot);
            if (stack.Count > MaxUndoHistoryCount)
            {
                stack.RemoveAt(0);
            }
        }

        private static EffectEditorHistorySnapshot PopHistorySnapshot(List<EffectEditorHistorySnapshot> stack)
        {
            if (stack.Count == 0)
            {
                return null;
            }

            int index = stack.Count - 1;
            EffectEditorHistorySnapshot snapshot = stack[index];
            stack.RemoveAt(index);
            return snapshot;
        }

        [RelayCommand]
        private void AddReanimTrack()
        {
            if (Kind != EffectAssetKind.Reanim || _reanimDefinition is null)
            {
                return;
            }

            RecordUndoSnapshot();
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
            RebuildReanimTweenViewModels();
            RebuildReanimViewModels(tracks.Length - 1, SelectedReanimFrameIndex);
            ApplyReanimPropertyChanges(recordUndo: false);
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

            RecordUndoSnapshot();
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
            RebuildReanimTweenViewModels();
            RebuildReanimViewModels(System.Math.Min(removeIndex, tracks.Length - 1), SelectedReanimFrameIndex);
            ApplyReanimPropertyChanges(recordUndo: false);
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
            RecordUndoSnapshot();
            for (int i = 0; i < count; i++)
            {
                InsertTransform(_reanimDefinition.mTracks[i], insertIndex, CreateDefaultReanimTransform(false));
            }

            InsertReanimTweenFrame(insertIndex);
            NormalizeReanimDefinition(_reanimDefinition);
            NormalizeReanimTweenMetadata();
            ApplyAllReanimTweens();
            RebuildReanimTweenViewModels();
            RebuildReanimViewModels(SelectedReanimTrack?.Index ?? 0, insertIndex);
            ApplyReanimPropertyChanges(recordUndo: false);
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
            RecordUndoSnapshot();
            for (int i = 0; i < count; i++)
            {
                RemoveTransform(_reanimDefinition.mTracks[i], removeIndex);
            }

            RemoveReanimTweenFrame(removeIndex);
            NormalizeReanimDefinition(_reanimDefinition);
            NormalizeReanimTweenMetadata();
            ApplyAllReanimTweens();
            RebuildReanimTweenViewModels();
            RebuildReanimViewModels(SelectedReanimTrack?.Index ?? 0, System.Math.Min(removeIndex, ReanimFrameCount - 1));
            ApplyReanimPropertyChanges(recordUndo: false);
        }

        [RelayCommand]
        private void RecognizeReanimTweens()
        {
            if (Kind != EffectAssetKind.Reanim || _reanimDefinition?.mTracks is null || _reanimAsset is null)
            {
                return;
            }

            RecordUndoSnapshot();
            _reanimAsset.Tweens = InferReanimTweens(_reanimDefinition, ResolveTransformPointScale);
            NormalizeReanimTweenMetadata();
            RebuildReanimTweenViewModels();
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

            RecordUndoSnapshot();
            _reanimAsset.Tweens.Add(tween);
            NormalizeReanimTweenMetadata();
            if (_reanimAsset.Tweens.Contains(tween))
            {
                ApplyReanimTween(tween);
            }

            RebuildReanimTweenViewModels();
            RefreshReanimTimelineCells();
            RaiseReanimTweenPropertiesChanged();
            ApplyReanimPropertyChanges(recordUndo: false);
        }

        [RelayCommand]
        private void RemoveReanimTween()
        {
            if (SelectedReanimTween is null || _reanimAsset?.Tweens is null)
            {
                return;
            }

            RecordUndoSnapshot();
            if (!_reanimAsset.Tweens.Remove(SelectedReanimTween.Model))
            {
                return;
            }

            RebuildReanimTweenViewModels();
            RefreshReanimTimelineCells();
            RaiseReanimTweenPropertiesChanged();
            ApplyReanimPropertyChanges(recordUndo: false);
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

            RecordUndoSnapshot();
            List<ReanimTween> tweens = CloneReanimTweens(_reanimAsset.Tweens);
            foreach (ReanimTween tween in tweens)
            {
                ApplyReanimTween(tween);
            }

            _reanimAsset.Tweens.Clear();
            RebuildReanimTweenViewModels();
            RefreshReanimTimelineCells();
            RaiseReanimTweenPropertiesChanged();
            ApplyReanimPropertyChanges(recordUndo: false);
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

            RecordUndoSnapshot();
            foreach (ReanimTween tween in containingTweens)
            {
                BakeReanimTween(tween, removeTween: true, refresh: false, markDirty: false, recordUndo: false);
                AddSplitReanimTween(tween, tween.StartFrame, frameIndex);
                AddSplitReanimTween(tween, frameIndex, tween.EndFrame);
            }

            NormalizeReanimTweenMetadata();
            ApplyAllReanimTweens();
            RebuildReanimTweenViewModels();
            RefreshReanimTimelineCells();
            RaiseReanimTweenPropertiesChanged();
            ApplyReanimPropertyChanges(recordUndo: false);
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

            RecordUndoSnapshot();
            ParticleEmitterDefinition[] emitters = _particleDefinition.mEmitterDefs ?? [];
            int index = _particleDefinition.mEmitterDefCount;
            System.Array.Resize(ref emitters, index + 1);
            emitters[index] = CreateDefaultEmitter(index);
            _particleDefinition.mEmitterDefs = emitters;
            _particleDefinition.mEmitterDefCount = emitters.Length;
            RebuildParticleEmitterViewModels(index);
            ApplyParticlePropertyChanges(recordUndo: false);
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

            RecordUndoSnapshot();
            ParticleEmitterDefinition[] emitters = new ParticleEmitterDefinition[count - 1];
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
            ApplyParticlePropertyChanges(recordUndo: false);
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
            RebuildReanimTweenViewModels();
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

            RecordUndoSnapshot();
            _reanimDefinition.mTracks[track.Index].mName = track.Name ?? string.Empty;
            UpdateReanimTweenTrackName(track.Index, track.Name);
            RefreshReanimTweenTrackNames();
            RefreshReanimLayers();
            ApplyReanimPropertyChanges(recordUndo: false);
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
                throw new InvalidOperationException(T("EffectEditor.AssetIdEmpty"));
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
                project.Definitions.SetReanimDefinition(Path, _reanimDefinition);
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
                ParticleDefinitionCodec.Encode(particleStream, _particleDefinition, particleFullPath);
                project.Definitions.SetParticleDefinition(Path, _particleDefinition);
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
            project.Definitions.SetTrailDefinition(Path, _trailDefinition);
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
                RebuildReanimTweenViewModels();

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

            ClearUndoRedoHistory();
            base.DiscardChanges();
        }

        private void InitializeTrailEditor()
        {
            _trailDefinition = _project?.Definitions?.GetTrailDefinitionCloneByPath(Path) ?? new TrailDefinition();
            _trailDefinition.ApplyDefaults();

            SetTrailProperties(
                _trailDefinition.mImage ?? string.Empty,
                _trailDefinition.mMaxPoints,
                _trailDefinition.mMinPointDistance,
                EffectUtility.TestBit((uint)_trailDefinition.mTrailFlags, (int)TrailFlags.Loops),
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
            ParticleDefinition definition = LoadParticleDefinitionFromFile();
            LoadParticleDefinition(definition, markDirty: false);
            AcceptSavedState();
            RefreshParticlePreview();
            OnPropertyChanged(nameof(HasParticleControls));
        }

        private ParticleDefinition LoadParticleDefinitionFromFile()
        {
            ParticleDefinition cached = _project?.Definitions?.GetParticleDefinitionClone(AssetId) ??
                _project?.Definitions?.GetParticleDefinitionCloneByPath(Path);
            if (cached is not null)
            {
                return cached;
            }

            string fullPath = ResolveEffectPath(_project, Path, _project.Assets.Particles.TryGetValue(AssetId, out EffectAsset asset) ? asset : null);
            if (string.IsNullOrWhiteSpace(fullPath) || !File.Exists(fullPath))
            {
                return ParticleDefinitionUtility.CreateEmpty();
            }

            using FileStream stream = File.OpenRead(fullPath);
            return ParticleDefinitionCodec.Decode(stream) ?? ParticleDefinitionUtility.CreateEmpty();
        }

        private ReanimatorDefinition LoadReanimDefinitionFromFile()
        {
            ReanimatorDefinition cached = _project?.Definitions?.GetReanimDefinitionClone(AssetId) ??
                _project?.Definitions?.GetReanimDefinitionCloneByPath(Path);
            if (cached is not null)
            {
                return cached;
            }

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
            ReanimLayers.Add(T("EffectEditor.FullTimeline"));
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

        private int GetFullReanimTimelineFrameCount()
        {
            if (_reanimDefinition?.mTracks is null || _reanimDefinition.mTrackCount <= 0)
            {
                return 0;
            }

            return System.Math.Max(0, (int)(_reanimDefinition.mTracks[0]?.mTransformCount ?? 0));
        }

        private bool TryGetReanimLayerFrameRange(string layerName, out int frameStart, out int frameCount)
        {
            frameStart = 0;
            frameCount = 0;
            if (_reanimDefinition?.mTracks is null || string.IsNullOrWhiteSpace(layerName))
            {
                return false;
            }

            ReanimatorTrack track = _reanimDefinition.mTracks
                .Take(_reanimDefinition.mTrackCount)
                .FirstOrDefault(candidate => string.Equals(candidate?.mName, layerName, System.StringComparison.OrdinalIgnoreCase));
            if (track?.mTransforms is null || track.mTransformCount <= 0)
            {
                return false;
            }

            frameCount = 1;
            bool foundStart = false;
            int count = System.Math.Min(track.mTransformCount, track.mTransforms.Length);
            for (int i = 0; i < count; i++)
            {
                if (track.mTransforms[i].mFrame >= 0f)
                {
                    frameStart = i;
                    foundStart = true;
                    break;
                }
            }

            if (!foundStart)
            {
                return true;
            }

            for (int i = frameStart; i < count; i++)
            {
                if (track.mTransforms[i].mFrame >= 0f)
                {
                    frameCount = i - frameStart + 1;
                }
            }

            return true;
        }

        private bool IsFullReanimLayer(string layerName)
        {
            return string.IsNullOrWhiteSpace(layerName) ||
                string.Equals(layerName, T("EffectEditor.FullTimeline"), System.StringComparison.Ordinal);
        }

        private void SelectReanimFrame(int frameIndex)
        {
            SelectReanimTimelineFrame(frameIndex);
        }

        public void SelectReanimTimelineFrame(int frameIndex)
        {
            ReanimIsPlaying = false;
            SelectedReanimFrameIndex = frameIndex;
            SelectReanimTweenForCurrentCell();
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

            ReanimTweenViewModel tween = FindReanimTweenForCell(trackIndex, frameIndex);
            if (ReferenceEquals(tween, SelectedReanimTween))
            {
                return;
            }

            try
            {
                _suppressReanimTweenSelection = true;
                SelectedReanimTween = tween;
            }
            finally
            {
                _suppressReanimTweenSelection = false;
            }
        }

        private void SelectReanimTweenForCurrentCell()
        {
            SelectReanimTweenForCell(SelectedReanimTrack?.Index ?? -1, SelectedReanimFrameIndex);
        }

        private ReanimTweenViewModel FindReanimTweenForCell(int trackIndex, int frameIndex)
        {
            if (trackIndex < 0)
            {
                return null;
            }

            return ReanimTweens
                .Where(item =>
                    item.Model.TrackIndex == trackIndex &&
                    frameIndex >= item.Model.StartFrame &&
                    frameIndex <= item.Model.EndFrame)
                .OrderByDescending(item => item.Model.StartFrame)
                .ThenBy(item => item.Model.EndFrame)
                .FirstOrDefault();
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
            OnPropertyChanged(nameof(TransformBox));
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

            RecordUndoSnapshot();
            transform.mImage = string.IsNullOrWhiteSpace(SelectedReanimImageId) ? null : SelectedReanimImageId.Trim();
            transform.mFont = string.IsNullOrWhiteSpace(SelectedReanimFontId) ? null : SelectedReanimFontId.Trim();
            transform.mText = SelectedReanimText ?? string.Empty;
            SetSelectedReanimTransform(transform);
            RefreshSelectedFrameCell();
            LoadTransformDialog();
            OnPropertyChanged(nameof(CanTransformSelectedReanimFrame));
            OnPropertyChanged(nameof(CanDrag));
            OnPropertyChanged(nameof(TransformBox));
            ApplyReanimPropertyChanges(recordUndo: false);
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

        private void ApplyReanimPropertyChanges(bool markDirty = true, bool recordUndo = true)
        {
            if (_reanimDefinition is null || _suppressReanimPropertyChanges)
            {
                return;
            }

            if (markDirty && recordUndo)
            {
                RecordUndoSnapshot();
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

        private void OnReanimPreviewCurrentFrameIndexChanged(object sender, int frameIndex)
        {
            if (Dispatcher.UIThread.CheckAccess())
            {
                SyncSelectedReanimFrameFromPlayback(frameIndex);
                return;
            }

            Dispatcher.UIThread.Post(() => SyncSelectedReanimFrameFromPlayback(frameIndex));
        }

        private void SyncSelectedReanimFrameFromPlayback(int frameIndex)
        {
            if (!ReanimIsPlaying || _reanimDefinition is null || ReanimFrameCount <= 0)
            {
                return;
            }

            int clamped = System.Math.Clamp(frameIndex, 0, ReanimFrameCount - 1);
            if (clamped == SelectedReanimFrameIndex)
            {
                return;
            }

            try
            {
                _syncingReanimPlaybackFrame = true;
                SelectedReanimFrameIndex = clamped;
            }
            finally
            {
                _syncingReanimPlaybackFrame = false;
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
            _reanimPreview.SetLayer(IsFullReanimLayer(SelectedReanimLayer) ? null : SelectedReanimLayer);
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

        public void BeginDrag(ViewportDragHandle handle, Vector2 worldPosition)
        {
            _activeViewportDragHandle = handle;
            _viewportDragStartWorld = worldPosition;
            if (!TryGetSelectedReanimTransform(out _viewportDragStartTransform))
            {
                _activeViewportDragHandle = ViewportDragHandle.None;
                _viewportDragStartSize = Vector2.One;
                return;
            }

            BeginUndoBatch();
            _viewportDragStartSize = ResolveTransformPointScale(_viewportDragStartTransform);
            if (_viewportDragStartSize.X <= 0f || !float.IsFinite(_viewportDragStartSize.X))
            {
                _viewportDragStartSize.X = 1f;
            }

            if (_viewportDragStartSize.Y <= 0f || !float.IsFinite(_viewportDragStartSize.Y))
            {
                _viewportDragStartSize.Y = 1f;
            }

        }

        public void DragTo(Vector2 worldPosition)
        {
            if (_activeViewportDragHandle == ViewportDragHandle.None ||
                !CanTransformSelectedReanimFrame ||
                !TryGetSelectedReanimTransform(out _))
            {
                return;
            }

            ReanimatorTransform transform = _viewportDragStartTransform;
            Vector2 worldDelta = worldPosition - _viewportDragStartWorld;
            RecordUndoSnapshot();
            switch (_activeViewportDragHandle)
            {
                case ViewportDragHandle.Move:
                    transform.mTransX = (float)RoundToThreeDecimals(_viewportDragStartTransform.mTransX + worldDelta.X);
                    transform.mTransY = (float)RoundToThreeDecimals(_viewportDragStartTransform.mTransY + worldDelta.Y);
                    break;
                case ViewportDragHandle.ScaleTopLeft:
                case ViewportDragHandle.ScaleTop:
                case ViewportDragHandle.ScaleTopRight:
                case ViewportDragHandle.ScaleRight:
                case ViewportDragHandle.ScaleBottomRight:
                case ViewportDragHandle.ScaleBottom:
                case ViewportDragHandle.ScaleBottomLeft:
                case ViewportDragHandle.ScaleLeft:
                    ApplyScaleDrag(ref transform, worldPosition);
                    break;
                case ViewportDragHandle.SkewTop:
                case ViewportDragHandle.SkewRight:
                case ViewportDragHandle.SkewBottom:
                case ViewportDragHandle.SkewLeft:
                    ApplySkewDrag(ref transform, worldDelta);
                    break;
            }

            SetSelectedReanimTransform(transform);
            ApplyReanimTweensForSelection();
            RefreshReanimTimelineCells();
            LoadTransformDialog();
            OnPropertyChanged(nameof(TransformBox));
            ApplyReanimPropertyChanges(recordUndo: false);
        }

        public void EndDrag()
        {
            _activeViewportDragHandle = ViewportDragHandle.None;
            EndUndoBatch();
        }

        private void ApplyScaleDrag(ref ReanimatorTransform transform, Vector2 worldPosition)
        {
            Vector2 local = WorldToTransformLocal(_viewportDragStartTransform, worldPosition);
            if (_viewportDragStartSize.X <= 0.001f || _viewportDragStartSize.Y <= 0.001f)
            {
                return;
            }

            bool scaleLeft = _activeViewportDragHandle is ViewportDragHandle.ScaleTopLeft or ViewportDragHandle.ScaleBottomLeft or ViewportDragHandle.ScaleLeft;
            bool scaleRight = _activeViewportDragHandle is ViewportDragHandle.ScaleTopRight or ViewportDragHandle.ScaleBottomRight or ViewportDragHandle.ScaleRight;
            bool scaleTop = _activeViewportDragHandle is ViewportDragHandle.ScaleTopLeft or ViewportDragHandle.ScaleTop or ViewportDragHandle.ScaleTopRight;
            bool scaleBottom = _activeViewportDragHandle is ViewportDragHandle.ScaleBottomLeft or ViewportDragHandle.ScaleBottom or ViewportDragHandle.ScaleBottomRight;

            float scaleX = _viewportDragStartTransform.mScaleX;
            float scaleY = _viewportDragStartTransform.mScaleY;
            Vector2 fixedPoint = _viewportDragStartSize * 0.5f;
            if (scaleLeft)
            {
                scaleX = (float)RoundToThreeDecimals(_viewportDragStartTransform.mScaleX * (1f - local.X / _viewportDragStartSize.X));
                fixedPoint.X = _viewportDragStartSize.X;
            }
            else if (scaleRight)
            {
                scaleX = (float)RoundToThreeDecimals(_viewportDragStartTransform.mScaleX * (local.X / _viewportDragStartSize.X));
                fixedPoint.X = 0f;
            }

            if (scaleTop)
            {
                scaleY = (float)RoundToThreeDecimals(_viewportDragStartTransform.mScaleY * (1f - local.Y / _viewportDragStartSize.Y));
                fixedPoint.Y = _viewportDragStartSize.Y;
            }
            else if (scaleBottom)
            {
                scaleY = (float)RoundToThreeDecimals(_viewportDragStartTransform.mScaleY * (local.Y / _viewportDragStartSize.Y));
                fixedPoint.Y = 0f;
            }

            transform.mScaleX = scaleX;
            transform.mScaleY = scaleY;
            KeepTransformPointFixed(ref transform, _viewportDragStartTransform, fixedPoint);
        }

        private void ApplySkewDrag(ref ReanimatorTransform transform, Vector2 worldDelta)
        {
            Vector2 center = _viewportDragStartSize * 0.5f;
            switch (_activeViewportDragHandle)
            {
                case ViewportDragHandle.SkewTop:
                case ViewportDragHandle.SkewBottom:
                    transform.mSkewY = (float)RoundToThreeDecimals(_viewportDragStartTransform.mSkewY + worldDelta.X);
                    break;
                case ViewportDragHandle.SkewLeft:
                case ViewportDragHandle.SkewRight:
                    transform.mSkewX = (float)RoundToThreeDecimals(_viewportDragStartTransform.mSkewX + worldDelta.Y);
                    break;
            }

            KeepTransformPointFixed(ref transform, _viewportDragStartTransform, center);
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
                RecordUndoSnapshot();
                try
                {
                    _suppressUndoRecording = true;
                    SelectedReanimTrack.Name = ReanimTransformDialog.TrackName;
                }
                finally
                {
                    _suppressUndoRecording = false;
                }
            }
            else
            {
                RecordUndoSnapshot();
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
            OnPropertyChanged(nameof(TransformBox));
            ApplyReanimPropertyChanges(recordUndo: false);
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
            OnPropertyChanged(nameof(TransformBox));
        }

        private void UpdateSelectedReanimTrackState()
        {
            foreach (ReanimTrackViewModel track in ReanimTracks)
            {
                track.IsSelected = ReferenceEquals(track, SelectedReanimTrack);
            }

            OnPropertyChanged(nameof(CanRemoveReanimTrack));
            OnPropertyChanged(nameof(HasSelectedReanimFrame));
            OnPropertyChanged(nameof(TransformBox));
        }

        private void RebuildReanimTweenViewModels()
        {
            try
            {
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
                            OnReanimTweenChanging,
                            OnReanimTweenChanged));
                    }
                }

                SelectedReanimTween = FindReanimTweenForCell(SelectedReanimTrack?.Index ?? -1, SelectedReanimFrameIndex);
            }
            finally
            {
                _suppressReanimTweenSelection = false;
            }

            OnPropertyChanged(nameof(ReanimTweens));
            RaiseReanimTweenPropertiesChanged();
        }

        private void OnReanimTweenChanging(ReanimTweenViewModel tween)
        {
            RecordUndoSnapshot();
        }

        private void RefreshReanimTweenTrackNames()
        {
            foreach (ReanimTweenViewModel tween in ReanimTweens)
            {
                tween.RefreshTrackName();
            }
        }

        private void RefreshReanimTweenViewModels()
        {
            if (_reanimAsset?.Tweens is null)
            {
                return;
            }

            for (int i = 0; i < ReanimTweens.Count; i++)
            {
                ReanimTweenViewModel tween = ReanimTweens[i];
                int modelIndex = _reanimAsset.Tweens.IndexOf(tween.Model);
                if (modelIndex < 0)
                {
                    continue;
                }

                tween.SetIndex(modelIndex);
                tween.RefreshModelState();
            }
        }

        private void OnReanimTweenChanged(ReanimTweenViewModel tween)
        {
            ReanimTween model = tween?.Model;
            NormalizeReanimTweenMetadata();
            ApplyAllReanimTweens();
            if (model is null ||
                _reanimAsset?.Tweens is null ||
                !_reanimAsset.Tweens.Contains(model) ||
                ReanimTweens.Count != _reanimAsset.Tweens.Count ||
                ReanimTweens.Any(item => !_reanimAsset.Tweens.Contains(item.Model)))
            {
                RebuildReanimTweenViewModels();
            }
            else
            {
                RefreshReanimTweenViewModels();
                SelectReanimTweenForCurrentCell();
            }

            RefreshReanimTimelineCells();
            RaiseReanimTweenPropertiesChanged();
            ApplyReanimPropertyChanges(recordUndo: false);
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
            OnPropertyChanged(nameof(TransformBox));
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
            OnPropertyChanged(nameof(TransformBox));
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
                tween.AnchorX = float.IsFinite(tween.AnchorX) ? tween.AnchorX : 0.5f;
                tween.AnchorY = float.IsFinite(tween.AnchorY) ? tween.AnchorY : 0.5f;

                if (tween.EndFrame <= tween.StartFrame + 1)
                {
                    _reanimAsset.Tweens.RemoveAt(i);
                    continue;
                }

                tween.Properties = NormalizeTweenProperties(tween.Properties);
            }

            for (int trackIndex = 0; trackIndex < trackCount; trackIndex++)
            {
                ReanimTween previousTween = null;
                foreach (ReanimTween tween in _reanimAsset.Tweens
                    .Where(item => item.TrackIndex == trackIndex)
                    .OrderBy(item => item.StartFrame)
                    .ThenBy(item => item.EndFrame)
                    .ToList())
                {
                    if (previousTween is not null && tween.StartFrame < previousTween.EndFrame)
                    {
                        tween.StartFrame = previousTween.EndFrame;
                    }

                    if (tween.EndFrame <= tween.StartFrame + 1)
                    {
                        _reanimAsset.Tweens.Remove(tween);
                        continue;
                    }

                    previousTween = tween;
                }
            }

            _reanimAsset.Tweens = _reanimAsset.Tweens
                .OrderBy(tween => tween.TrackIndex)
                .ThenBy(tween => tween.StartFrame)
                .ThenBy(tween => tween.EndFrame)
                .ToList();
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
            Vector2 anchor = new(tween.AnchorX, tween.AnchorY);
            Vector2 startTransformPoint = ResolveAnchorTransformPoint(start, anchor);
            Vector2 endTransformPoint = ResolveAnchorTransformPoint(end, anchor);
            Vector2 startPoint = TransformPoint(start, startTransformPoint);
            Vector2 endPoint = TransformPoint(end, endTransformPoint);
            tween.Properties = NormalizeTweenProperties(tween.Properties);
            int span = tween.EndFrame - tween.StartFrame;
            for (int frameIndex = tween.StartFrame + 1; frameIndex < tween.EndFrame; frameIndex++)
            {
                float fraction = (frameIndex - tween.StartFrame) / (float)span;
                ReanimatorTransform transform = track.mTransforms[frameIndex];
                transform.mSkewX = Lerp(start.mSkewX, end.mSkewX, fraction);
                transform.mSkewY = Lerp(start.mSkewY, end.mSkewY, fraction);
                transform.mScaleX = Lerp(start.mScaleX, end.mScaleX, fraction);
                transform.mScaleY = Lerp(start.mScaleY, end.mScaleY, fraction);
                transform.mAlpha = Lerp(start.mAlpha, end.mAlpha, fraction);
                transform.mFrame = start.mFrame;
                transform.mImage = start.mImage;
                transform.mFont = start.mFont;
                transform.mText = start.mText;
                Vector2 expectedPoint = Vector2.Lerp(startPoint, endPoint, fraction);
                Vector2 transformPoint = ResolveAnchorTransformPoint(transform, anchor);
                Vector2 transformedPointOffset = TransformPointOffset(transform, transformPoint);
                transform.mTransX = expectedPoint.X - transformedPointOffset.X;
                transform.mTransY = expectedPoint.Y - transformedPointOffset.Y;
                track.mTransforms[frameIndex] = transform;
            }
        }

        private void BakeReanimTween(
            ReanimTween tween,
            bool removeTween,
            bool refresh = true,
            bool markDirty = true,
            bool recordUndo = true)
        {
            if (tween is null)
            {
                return;
            }

            if (markDirty && recordUndo)
            {
                RecordUndoSnapshot();
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
            RebuildReanimTweenViewModels();
            RefreshReanimTimelineCells();
            RaiseReanimTweenPropertiesChanged();
            ApplyReanimPropertyChanges(markDirty, recordUndo: false);
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
                AnchorX = source.AnchorX,
                AnchorY = source.AnchorY,
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
                    AnchorX = tween.AnchorX,
                    AnchorY = tween.AnchorY,
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

        private static List<ReanimTween> InferReanimTweens(
            ReanimatorDefinition definition,
            Func<ReanimatorTransform, Vector2> transformPointResolver)
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

                AddInferredTweens(tweens, track, trackIndex, transformPointResolver);
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
            int trackIndex,
            Func<ReanimatorTransform, Vector2> transformPointResolver)
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

                int end = FindLinearTweenRunEnd(track.mTransforms, count, start, transformPointResolver, out Vector2 anchor);
                if (end > start + 1 &&
                    HasConstantTweenResourceFields(track.mTransforms, start, end))
                {
                    tweens.Add(new ReanimTween
                    {
                        TrackIndex = trackIndex,
                        TrackName = track.mName ?? string.Empty,
                        StartFrame = start,
                        EndFrame = end,
                        AnchorX = anchor.X,
                        AnchorY = anchor.Y,
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
            int start,
            Func<ReanimatorTransform, Vector2> transformPointResolver,
            out Vector2 anchor)
        {
            ReanimatorTransform first = transforms[start];

            anchor = new Vector2(0.5f, 0.5f);
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

                if (TryFitLinearTweenAnchor(transforms, start, candidateEnd, transformPointResolver, out Vector2 fittedAnchor))
                {
                    end = candidateEnd;
                    anchor = fittedAnchor;
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

        private static bool TryFitLinearTweenAnchor(
            ReanimatorTransform[] transforms,
            int startFrame,
            int endFrame,
            Func<ReanimatorTransform, Vector2> transformPointResolver,
            out Vector2 anchor)
        {
            anchor = new Vector2(0.5f, 0.5f);
            if (IsLinearTweenRun(transforms, startFrame, endFrame, anchor, transformPointResolver))
            {
                return true;
            }

            if (!TrySolveTweenAnchor(transforms, startFrame, endFrame, transformPointResolver, out anchor))
            {
                return false;
            }

            if (!IsLinearTweenRun(transforms, startFrame, endFrame, anchor, transformPointResolver))
            {
                return false;
            }

            Vector2 roundedAnchor = new(
                (float)RoundToThreeDecimals(anchor.X),
                (float)RoundToThreeDecimals(anchor.Y));
            if (IsLinearTweenRun(transforms, startFrame, endFrame, roundedAnchor, transformPointResolver))
            {
                anchor = roundedAnchor;
            }

            return true;
        }

        private static bool TrySolveTweenAnchor(
            ReanimatorTransform[] transforms,
            int startFrame,
            int endFrame,
            Func<ReanimatorTransform, Vector2> transformPointScaleResolver,
            out Vector2 anchor)
        {
            anchor = new Vector2(0.5f, 0.5f);
            int span = endFrame - startFrame;
            if (span < 2)
            {
                return false;
            }

            MatrixFromTransformWithoutTranslation(transforms[startFrame], out Matrix4x4 startMatrix);
            MatrixFromTransformWithoutTranslation(transforms[endFrame], out Matrix4x4 endMatrix);
            Vector2 startScale = transformPointScaleResolver?.Invoke(transforms[startFrame]) ?? Vector2.One;
            Vector2 endScale = transformPointScaleResolver?.Invoke(transforms[endFrame]) ?? Vector2.One;

            double ata00 = 0d;
            double ata01 = 0d;
            double ata11 = 0d;
            double atb0 = 0d;
            double atb1 = 0d;

            for (int frameIndex = startFrame + 1; frameIndex < endFrame; frameIndex++)
            {
                float fraction = (frameIndex - startFrame) / (float)span;
                ReanimatorTransform transform = transforms[frameIndex];
                MatrixFromTransformWithoutTranslation(transform, out Matrix4x4 frameMatrix);
                Vector2 frameScale = transformPointScaleResolver?.Invoke(transform) ?? Vector2.One;

                AddAnchorEquation(
                    frameMatrix.M11 * frameScale.X - Lerp(startMatrix.M11 * startScale.X, endMatrix.M11 * endScale.X, fraction),
                    frameMatrix.M21 * frameScale.Y - Lerp(startMatrix.M21 * startScale.Y, endMatrix.M21 * endScale.Y, fraction),
                    Lerp(transforms[startFrame].mTransX, transforms[endFrame].mTransX, fraction) - transform.mTransX,
                    ref ata00,
                    ref ata01,
                    ref ata11,
                    ref atb0,
                    ref atb1);

                AddAnchorEquation(
                    frameMatrix.M12 * frameScale.X - Lerp(startMatrix.M12 * startScale.X, endMatrix.M12 * endScale.X, fraction),
                    frameMatrix.M22 * frameScale.Y - Lerp(startMatrix.M22 * startScale.Y, endMatrix.M22 * endScale.Y, fraction),
                    Lerp(transforms[startFrame].mTransY, transforms[endFrame].mTransY, fraction) - transform.mTransY,
                    ref ata00,
                    ref ata01,
                    ref ata11,
                    ref atb0,
                    ref atb1);
            }

            double determinant = (ata00 * ata11) - (ata01 * ata01);
            if (System.Math.Abs(determinant) < 0.000001d)
            {
                return false;
            }

            double anchorX = ((atb0 * ata11) - (atb1 * ata01)) / determinant;
            double anchorY = ((ata00 * atb1) - (ata01 * atb0)) / determinant;
            if (!double.IsFinite(anchorX) || !double.IsFinite(anchorY))
            {
                return false;
            }

            anchor = new Vector2((float)anchorX, (float)anchorY);
            return true;
        }

        private static void AddAnchorEquation(
            double coefficientX,
            double coefficientY,
            double value,
            ref double ata00,
            ref double ata01,
            ref double ata11,
            ref double atb0,
            ref double atb1)
        {
            ata00 += coefficientX * coefficientX;
            ata01 += coefficientX * coefficientY;
            ata11 += coefficientY * coefficientY;
            atb0 += coefficientX * value;
            atb1 += coefficientY * value;
        }

        private static bool IsLinearTweenRun(
            ReanimatorTransform[] transforms,
            int startFrame,
            int endFrame,
            Vector2 anchor,
            Func<ReanimatorTransform, Vector2> transformPointResolver)
        {
            ReanimatorTransform start = transforms[startFrame];
            ReanimatorTransform end = transforms[endFrame];
            Vector2 startTransformPoint = ResolveAnchorTransformPoint(start, anchor, transformPointResolver);
            Vector2 endTransformPoint = ResolveAnchorTransformPoint(end, anchor, transformPointResolver);
            Vector2 startPoint = TransformPoint(start, startTransformPoint);
            Vector2 endPoint = TransformPoint(end, endTransformPoint);
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
                if (!IsExpectedTweenTransform(start, end, startPoint, endPoint, anchor, transformPointResolver, transform, fraction))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsExpectedTweenTransform(
            ReanimatorTransform start,
            ReanimatorTransform end,
            Vector2 startPoint,
            Vector2 endPoint,
            Vector2 anchor,
            Func<ReanimatorTransform, Vector2> transformPointResolver,
            ReanimatorTransform transform,
            float fraction)
        {
            Vector2 transformPoint = ResolveAnchorTransformPoint(transform, anchor, transformPointResolver);
            Vector2 actualPoint = TransformPoint(transform, transformPoint);
            Vector2 expectedPoint = Vector2.Lerp(startPoint, endPoint, fraction);
            return NearlyEqual(actualPoint.X, expectedPoint.X, 0.15f) &&
                NearlyEqual(actualPoint.Y, expectedPoint.Y, 0.15f) &&
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

        private Vector2 ResolveAnchorTransformPoint(ReanimatorTransform transform, Vector2 anchor)
        {
            Vector2 size = ResolveTransformPointScale(transform);
            return new Vector2(anchor.X * size.X, anchor.Y * size.Y);
        }

        private bool TryGetSelectedTransformBox(out ViewportTransformBox box)
        {
            box = default;
            if (!CanDrag || !TryGetSelectedReanimTransform(out ReanimatorTransform transform))
            {
                return false;
            }

            Vector2 size = ResolveTransformPointScale(transform);
            box = new ViewportTransformBox(
                TransformPoint(transform, Vector2.Zero),
                TransformPoint(transform, new Vector2(size.X, 0f)),
                TransformPoint(transform, size),
                TransformPoint(transform, new Vector2(0f, size.Y)));
            return true;
        }

        private Vector2 ResolveTransformPointScale(ReanimatorTransform transform)
        {
            Image image = ResolveImage(transform.mImage);
            return image is null
                ? Vector2.One
                : new Vector2(image.GetCelWidth(), image.GetCelHeight());
        }

        private static Vector2 ResolveAnchorTransformPoint(
            ReanimatorTransform transform,
            Vector2 anchor,
            Func<ReanimatorTransform, Vector2> transformPointScaleResolver)
        {
            Vector2 scale = transformPointScaleResolver?.Invoke(transform) ?? Vector2.One;
            return new Vector2(anchor.X * scale.X, anchor.Y * scale.Y);
        }

        private Image ResolveImage(string imageId)
        {
            if (string.IsNullOrWhiteSpace(imageId) ||
                _project?.Assets?.TryGetImage(imageId, out ImageAsset asset) != true)
            {
                return null;
            }

            if (_imageSizeCache.TryGetValue(asset.Id, out Image cachedImage))
            {
                return cachedImage;
            }

            string fullPath = ProjectPathUtility.ResolvePath(_project, asset.Path);
            Image image = new()
            {
                mId = asset.Id,
                mNumRows = System.Math.Max(1, asset.Rows),
                mNumCols = System.Math.Max(1, asset.Cols)
            };

            if (ImageFileSizeReader.TryReadSize(fullPath, out int width, out int height))
            {
                image.mWidth = width;
                image.mHeight = height;
            }

            _imageSizeCache[asset.Id] = image;
            return image;
        }

        private static Vector2 TransformPoint(ReanimatorTransform transform, Vector2 point)
        {
            Vector2 offset = TransformPointOffset(transform, point);
            return new Vector2(transform.mTransX + offset.X, transform.mTransY + offset.Y);
        }

        private static Vector2 TransformPointOffset(ReanimatorTransform transform, Vector2 point)
        {
            MatrixFromTransformWithoutTranslation(transform, out Matrix4x4 matrix);
            return Vector2.Transform(point, matrix);
        }

        private static Vector2 WorldToTransformLocal(ReanimatorTransform transform, Vector2 worldPoint)
        {
            Reanimation.MatrixFromTransform(transform, out Matrix4x4 matrix);
            if (!Matrix4x4.Invert(matrix, out Matrix4x4 inverse))
            {
                return Vector2.Zero;
            }

            return Vector2.Transform(worldPoint, inverse);
        }

        private static void KeepTransformPointFixed(
            ref ReanimatorTransform transform,
            ReanimatorTransform originalTransform,
            Vector2 transformPoint)
        {
            Vector2 fixedWorldPoint = TransformPoint(originalTransform, transformPoint);
            Vector2 newOffset = TransformPointOffset(transform, transformPoint);
            transform.mTransX = (float)RoundToThreeDecimals(fixedWorldPoint.X - newOffset.X);
            transform.mTransY = (float)RoundToThreeDecimals(fixedWorldPoint.Y - newOffset.Y);
        }

        private static void MatrixFromTransformWithoutTranslation(in ReanimatorTransform transform, out Matrix4x4 matrix)
        {
            Reanimation.MatrixFromTransform(transform, out matrix);
            matrix.M41 = 0f;
            matrix.M42 = 0f;
            matrix.M43 = 0f;
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
            return value == ReanimatorUtility.DEFAULT_FIELD_PLACEHOLDER;
        }

        private static double RoundToThreeDecimals(double value)
        {
            double rounded = System.Math.Round(value, 3, System.MidpointRounding.AwayFromZero);
            return rounded == -0d ? 0d : rounded;
        }

        private void LoadParticleDefinition(ParticleDefinition definition, bool markDirty)
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

        private static ParticleEmitterDefinition CreateDefaultEmitter(int index)
        {
            return new ParticleEmitterDefinition
            {
                mName = F("EffectEditor.DefaultEmitterName", index + 1)
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

        private void ApplyTrailPropertyChanges(bool markDirty = true, bool recordUndo = true)
        {
            if (_trailDefinition is null || _suppressTrailPropertyChanges)
            {
                return;
            }

            if (markDirty && recordUndo)
            {
                RecordUndoSnapshot();
            }

            _trailDefinition.mImage = string.IsNullOrWhiteSpace(TrailImageId) ? null : TrailImageId.Trim();
            _trailDefinition.mMaxPoints = System.Math.Clamp(TrailMaxPoints, 2, EffectConstants.MAX_TRAIL_POINTS);
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

        private void ApplyParticlePropertyChanges(bool markDirty = true, bool recordUndo = true)
        {
            if (_particleDefinition is null || _suppressParticlePropertyChanges)
            {
                return;
            }

            if (markDirty && recordUndo)
            {
                RecordUndoSnapshot();
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

        private static bool IsTrailFlagSet(TrailDefinition definition, TrailFlags flag)
        {
            return definition is not null &&
                (definition.mTrailFlags & (1 << (int)flag)) != 0;
        }

        private static TrailDefinition CloneTrailDefinition(TrailDefinition source)
        {
            if (source is null)
            {
                return null;
            }

            TrailDefinition clone = new()
            {
                mImage = source.mImage,
                mMaxPoints = source.mMaxPoints,
                mMinPointDistance = source.mMinPointDistance,
                mTrailFlags = source.mTrailFlags
            };
            CopyTrack(source.mWidthOverLength, clone.mWidthOverLength);
            CopyTrack(source.mAlphaOverLength, clone.mAlphaOverLength);
            CopyTrack(source.mWidthOverTime, clone.mWidthOverTime);
            CopyTrack(source.mAlphaOverTime, clone.mAlphaOverTime);
            CopyTrack(source.mTrailDuration, clone.mTrailDuration);
            return clone;
        }

        private static FloatParameterTrack CloneTrack(FloatParameterTrack source)
        {
            FloatParameterTrack clone = new();
            CopyTrack(source, clone);
            return clone;
        }

        private static void CopyTrack(FloatParameterTrack source, FloatParameterTrack target)
        {
            if (target is null)
            {
                return;
            }

            if (source?.mNodes is null || source.mCountNodes <= 0)
            {
                target.mNodes = [];
                target.mCountNodes = 0;
                return;
            }

            int count = System.Math.Min(source.mCountNodes, source.mNodes.Length);
            target.mNodes = new FloatParameterTrackNode[count];
            target.mCountNodes = count;
            for (int i = 0; i < count; i++)
            {
                FloatParameterTrackNode node = source.mNodes[i];
                target.mNodes[i] = new FloatParameterTrackNode
                {
                    mTime = node.mTime,
                    mLowValue = node.mLowValue,
                    mHighValue = node.mHighValue,
                    mCurveType = node.mCurveType,
                    mDistribution = node.mDistribution
                };
            }
        }

        private static string ResolveEffectPath(EffectProject project, string path, EffectAsset asset)
        {
            string assetPath = asset?.Path;
            string effectivePath = string.IsNullOrWhiteSpace(assetPath) ? path : assetPath;
            return TrailPreviewFrameBuilder.ResolvePath(project, effectivePath);
        }

        private sealed class EffectEditorHistorySnapshot
        {
            public EffectAssetKind Kind { get; init; }
            public string AssetId { get; init; }
            public ReanimatorDefinition ReanimDefinition { get; init; }
            public List<ReanimTween> ReanimTweens { get; init; } = [];
            public int SelectedReanimTrackIndex { get; init; }
            public int SelectedReanimFrameIndex { get; init; }
            public ParticleDefinition ParticleDefinition { get; init; }
            public int SelectedParticleEmitterIndex { get; init; }
            public TrailDefinition TrailDefinition { get; init; }
        }

        public override void Dispose()
        {
            Loc.LanguageChanged -= OnLanguageChanged;
            if (_reanimPreview is not null)
            {
                _reanimPreview.CurrentFrameIndexChanged -= OnReanimPreviewCurrentFrameIndexChanged;
            }

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
