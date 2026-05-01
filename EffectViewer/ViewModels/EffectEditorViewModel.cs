using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using EffectViewer.Projects;
using EffectViewer.Rendering;
using EffectViewer.TodLib.Common;
using EffectViewer.TodLib.Particle;
using EffectViewer.TodLib.Trail;

namespace EffectViewer.ViewModels
{
    public sealed partial class EffectEditorViewModel : EditorViewModelBase
    {
        private readonly EffectProject _project;
        private readonly ReanimPreviewSimulation _reanimPreview;
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
        private ParticleEmitterViewModel _selectedParticleEmitter;
        private string _trailImageId;
        private int _trailMaxPoints;
        private double _trailMinPointDistance;
        private bool _trailLoops;
        private string _trailDefinitionError;
        private string _particleDefinitionError;
        private bool _suppressTrailPropertyChanges;
        private bool _suppressParticlePropertyChanges;

        public string AssetId { get; }
        public string Path { get; }
        public string EditorSummary { get; }
        public EffectFileSummary FileSummary { get; }
        public ObservableCollection<string> ReanimLayers { get; } = [];
        public ObservableCollection<ReanimTrackViewModel> ReanimTracks { get; } = [];
        public ObservableCollection<ParticleEmitterViewModel> ParticleEmitters { get; } = [];
        public bool IsReanimEditor => Kind == EffectAssetKind.Reanim;
        public bool IsParticleEditor => Kind == EffectAssetKind.Particle;
        public bool IsTrailEditor => Kind == EffectAssetKind.Trail;
        public bool HasReanimControls => Kind == EffectAssetKind.Reanim && ReanimTracks.Count > 0;
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

        public override bool SupportsSave => Kind == EffectAssetKind.Trail || Kind == EffectAssetKind.Particle;
        public override bool SupportsFileExport => true;
        public override string ExportPath => Path;
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

        private void OnParticleEmitterChanged(ParticleEmitterViewModel emitter)
        {
            ApplyParticlePropertyChanges();
        }

        public override async Task SaveAsync(EffectProjectService projectService, EffectProject project)
        {
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
            if (Kind == EffectAssetKind.Trail && _trailDefinition is not null)
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
            if (Kind == EffectAssetKind.Trail && _trailDefinition is not null)
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
