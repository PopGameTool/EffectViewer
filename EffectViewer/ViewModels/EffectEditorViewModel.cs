using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
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
        private ReanimatorDefinition _reanimDefinition;
        private ReanimatorDefinition _savedReanimDefinition;
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
        private bool _suppressTrailPropertyChanges;
        private bool _suppressParticlePropertyChanges;

        public string AssetId { get; }
        public string Path { get; }
        public string EditorSummary { get; }
        public EffectFileSummary FileSummary { get; }
        public ObservableCollection<string> ReanimLayers { get; } = [];
        public ObservableCollection<ReanimTrackViewModel> ReanimTracks { get; } = [];
        public ObservableCollection<ReanimFrameHeaderViewModel> ReanimFrameHeaders { get; } = [];
        public ObservableCollection<ParticleEmitterViewModel> ParticleEmitters { get; } = [];
        public bool IsReanimEditor => Kind == EffectAssetKind.Reanim;
        public bool IsParticleEditor => Kind == EffectAssetKind.Particle;
        public bool IsTrailEditor => Kind == EffectAssetKind.Trail;
        public bool HasReanimControls => Kind == EffectAssetKind.Reanim && ReanimTracks.Count > 0;
        public bool HasSelectedReanimFrame => IsReanimEditor && SelectedReanimTrack is not null && ReanimFrameCount > 0;
        public bool CanTransformSelectedReanimFrame => HasSelectedReanimFrame;
        public bool CanDrag => CanTransformSelectedReanimFrame && SelectedReanimFrameVisible;
        public bool CanRemoveReanimTrack => IsReanimEditor && ReanimTracks.Count > 1 && SelectedReanimTrack is not null;
        public bool CanRemoveReanimFrame => IsReanimEditor && ReanimFrameCount > 1;
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
            AssetId = assetId;
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
            NormalizeReanimDefinition(_reanimDefinition);
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

            NormalizeReanimDefinition(_reanimDefinition);
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

            NormalizeReanimDefinition(_reanimDefinition);
            RebuildReanimViewModels(SelectedReanimTrack?.Index ?? 0, System.Math.Min(removeIndex, ReanimFrameCount - 1));
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
            if (Kind == EffectAssetKind.Reanim && _reanimDefinition is not null)
            {
                string reanimFullPath = ResolveEffectPath(project, Path, project.Assets.Reanims.TryGetValue(AssetId, out EffectAsset asset) ? asset : null);
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
                await using FileStream reanimStream = File.Create(reanimFullPath);
                ReanimReader.Encode(reanimStream, _reanimDefinition);
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
                SexyParticleReader.Encode(particleStream, _particleDefinition);
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
            TrailReader.Encode(stream, _trailDefinition);
            AcceptSavedState();
        }

        public override void AcceptSavedState()
        {
            if (Kind == EffectAssetKind.Reanim && _reanimDefinition is not null)
            {
                NormalizeReanimDefinition(_reanimDefinition);
                _savedReanimDefinition = CloneReanimDefinition(_reanimDefinition);
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
            if (Kind == EffectAssetKind.Reanim && _savedReanimDefinition is not null)
            {
                _reanimDefinition = CloneReanimDefinition(_savedReanimDefinition);
                NormalizeReanimDefinition(_reanimDefinition);
                ReanimFps = _reanimDefinition.mFPS <= 0f ? 12d : _reanimDefinition.mFPS;
                RebuildReanimViewModels(
                    System.Math.Min(SelectedReanimTrack?.Index ?? 0, System.Math.Max(0, _reanimDefinition.mTrackCount - 1)),
                    System.Math.Min(SelectedReanimFrameIndex, System.Math.Max(0, ReanimFrameCount - 1)));
                ApplyReanimPreviewState();
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
            string fullPath = ResolveEffectPath(_project, Path, _project.Assets.Reanims.TryGetValue(AssetId, out EffectAsset asset) ? asset : null);
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
            ReanimFrameHeaders.Clear();
            int frameCount = ReanimFrameCount;
            for (int i = 0; i < frameCount; i++)
            {
                ReanimFrameHeaders.Add(new ReanimFrameHeaderViewModel(i, SelectReanimFrame));
            }

            if (_reanimDefinition?.mTracks is not null)
            {
                int count = System.Math.Min(_reanimDefinition.mTrackCount, _reanimDefinition.mTracks.Length);
                for (int trackIndex = 0; trackIndex < count; trackIndex++)
                {
                    ReanimatorTrack definitionTrack = _reanimDefinition.mTracks[trackIndex];
                    ReanimTrackViewModel track = new(trackIndex, definitionTrack?.mName ?? string.Empty);
                    track.VisibilityChanged += OnReanimTrackVisibilityChanged;
                    track.NameChanged += OnReanimTrackNameChanged;
                    for (int frameIndex = 0; frameIndex < frameCount; frameIndex++)
                    {
                        ReanimFrameCellViewModel cell = new(trackIndex, frameIndex, SelectReanimFrame);
                        UpdateFrameCell(cell);
                        track.Frames.Add(cell);
                    }

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
            RaiseReanimStructureChanged();
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
            ReanimIsPlaying = false;
            SelectedReanimFrameIndex = frameIndex;
        }

        private void SelectReanimFrame(int trackIndex, int frameIndex)
        {
            ReanimIsPlaying = false;
            if (trackIndex >= 0 && trackIndex < ReanimTracks.Count)
            {
                SelectedReanimTrack = ReanimTracks[trackIndex];
            }

            SelectedReanimFrameIndex = frameIndex;
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
            OnPropertyChanged(nameof(CanTransformSelectedReanimFrame));
            OnPropertyChanged(nameof(CanDrag));
            OnPropertyChanged(nameof(SelectedReanimFrameSummary));
        }

        private void ApplyReanimFrameResourceChanges()
        {
            if (_suppressReanimPropertyChanges || !TryGetSelectedReanimTransform(out ReanimatorTransform transform))
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
            SetSelectedReanimFrameResourceProperties(
                ReanimTransformDialog.Frame >= 0d,
                ReanimTransformDialog.ImageId,
                ReanimTransformDialog.FontId,
                ReanimTransformDialog.Text);
            RefreshSelectedFrameCell();
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
            if (SelectedReanimTrack is null ||
                SelectedReanimFrameIndex < 0 ||
                SelectedReanimFrameIndex >= SelectedReanimTrack.Frames.Count)
            {
                return;
            }

            UpdateFrameCell(SelectedReanimTrack.Frames[SelectedReanimFrameIndex]);
        }

        private void UpdateFrameCell(ReanimFrameCellViewModel cell)
        {
            bool hasContent = false;
            string imageId = string.Empty;
            if (_reanimDefinition?.mTracks is not null &&
                cell.TrackIndex >= 0 &&
                cell.TrackIndex < _reanimDefinition.mTrackCount &&
                cell.FrameIndex >= 0 &&
                cell.FrameIndex < _reanimDefinition.mTracks[cell.TrackIndex].mTransformCount)
            {
                ReanimatorTransform transform = _reanimDefinition.mTracks[cell.TrackIndex].mTransforms[cell.FrameIndex];
                hasContent = transform.mFrame >= 0f;
                imageId = transform.mImage ?? string.Empty;
            }

            cell.Update(hasContent, imageId);
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

        private void UpdateReanimTimelineSelection()
        {
            foreach (ReanimFrameHeaderViewModel header in ReanimFrameHeaders)
            {
                header.IsPlayhead = header.FrameIndex == SelectedReanimFrameIndex;
            }

            foreach (ReanimTrackViewModel track in ReanimTracks)
            {
                foreach (ReanimFrameCellViewModel frame in track.Frames)
                {
                    frame.IsPlayhead = frame.FrameIndex == SelectedReanimFrameIndex;
                    frame.IsSelected = ReferenceEquals(track, SelectedReanimTrack) &&
                        frame.FrameIndex == SelectedReanimFrameIndex;
                }
            }

            OnPropertyChanged(nameof(CanRemoveReanimFrame));
            OnPropertyChanged(nameof(SelectedReanimFrameSummary));
        }

        private void RaiseReanimStructureChanged()
        {
            OnPropertyChanged(nameof(ReanimTrackCount));
            OnPropertyChanged(nameof(ReanimFrameCount));
            OnPropertyChanged(nameof(ReanimDurationSummary));
            OnPropertyChanged(nameof(HasReanimControls));
            OnPropertyChanged(nameof(HasSelectedReanimFrame));
            OnPropertyChanged(nameof(CanTransformSelectedReanimFrame));
            OnPropertyChanged(nameof(CanDrag));
            OnPropertyChanged(nameof(CanRemoveReanimTrack));
            OnPropertyChanged(nameof(CanRemoveReanimFrame));
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
