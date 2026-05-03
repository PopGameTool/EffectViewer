using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EffectViewer.Localization;
using EffectViewer.TodLib.Common;
using EffectViewer.TodLib.Particle;

namespace EffectViewer.ViewModels
{
    public sealed partial class ParticleEmitterViewModel : ViewModelBase, IDisposable
    {
        private sealed class TrackBinding
        {
            public FloatParameterTrackViewModel ViewModel { get; init; }
            public Func<TodEmitterDefinition, FloatParameterTrack> Getter { get; init; }
            public float DefaultValue { get; init; }
            public bool IsDirty { get; set; }
        }

        private readonly List<TrackBinding> _trackBindings = [];
        private bool _suppressChanged;
        private string _name;
        private string _imageId;
        private int _imageRow;
        private int _imageCol;
        private int _imageFrames;
        private bool _animated;
        private EmitterType _emitterType;
        private bool _randomLaunchSpin;
        private bool _alignLaunchSpin;
        private bool _alignToPixels;
        private bool _systemLoops;
        private bool _particleLoops;
        private bool _particlesDontFollow;
        private bool _dieIfOverloaded;
        private bool _additive;
        private bool _fullscreen;
        private bool _softwareOnly;
        private bool _hardwareOnly;

        public int Index { get; private set; }
        public string DisplayName => string.IsNullOrWhiteSpace(Name)
            ? Loc.Format("Particle.EmitterFallbackName", Index + 1)
            : Loc.Format("Particle.EmitterDisplayName", Index + 1, Name);
        public string Detail => string.IsNullOrWhiteSpace(ImageId) ? Loc.Text("Particle.NoImage") : ImageId;
        public EmitterType[] EmitterTypeOptions { get; } = Enum.GetValues<EmitterType>();
        public ObservableCollection<ParticleFieldViewModel> ParticleFields { get; } = [];
        public ObservableCollection<ParticleFieldViewModel> SystemFields { get; } = [];

        public FloatParameterTrackViewModel SystemDuration { get; } = new("SystemDuration", 0f);
        public FloatParameterTrackViewModel CrossFadeDuration { get; } = new("CrossFadeDuration", 0f);
        public FloatParameterTrackViewModel SpawnRate { get; } = new("SpawnRate", 0f);
        public FloatParameterTrackViewModel SpawnMinActive { get; } = new("SpawnMinActive", -1f);
        public FloatParameterTrackViewModel SpawnMaxActive { get; } = new("SpawnMaxActive", -1f);
        public FloatParameterTrackViewModel SpawnMaxLaunched { get; } = new("SpawnMaxLaunched", -1f);
        public FloatParameterTrackViewModel EmitterRadius { get; } = new("EmitterRadius", 0f);
        public FloatParameterTrackViewModel EmitterOffsetX { get; } = new("EmitterOffsetX", 0f);
        public FloatParameterTrackViewModel EmitterOffsetY { get; } = new("EmitterOffsetY", 0f);
        public FloatParameterTrackViewModel EmitterBoxX { get; } = new("EmitterBoxX", 0f);
        public FloatParameterTrackViewModel EmitterBoxY { get; } = new("EmitterBoxY", 0f);
        public FloatParameterTrackViewModel EmitterSkewX { get; } = new("EmitterSkewX", 0f);
        public FloatParameterTrackViewModel EmitterSkewY { get; } = new("EmitterSkewY", 0f);
        public FloatParameterTrackViewModel EmitterPath { get; } = new("EmitterPath", 0f);
        public FloatParameterTrackViewModel ParticleDuration { get; } = new("ParticleDuration", 100f);
        public FloatParameterTrackViewModel LaunchSpeed { get; } = new("LaunchSpeed", 0f);
        public FloatParameterTrackViewModel LaunchAngle { get; } = new("LaunchAngle", 0f);
        public FloatParameterTrackViewModel SystemRed { get; } = new("SystemRed", 1f);
        public FloatParameterTrackViewModel SystemGreen { get; } = new("SystemGreen", 1f);
        public FloatParameterTrackViewModel SystemBlue { get; } = new("SystemBlue", 1f);
        public FloatParameterTrackViewModel SystemAlpha { get; } = new("SystemAlpha", 1f);
        public FloatParameterTrackViewModel SystemBrightness { get; } = new("SystemBrightness", 1f);
        public FloatParameterTrackViewModel ParticleRed { get; } = new("ParticleRed", 1f);
        public FloatParameterTrackViewModel ParticleGreen { get; } = new("ParticleGreen", 1f);
        public FloatParameterTrackViewModel ParticleBlue { get; } = new("ParticleBlue", 1f);
        public FloatParameterTrackViewModel ParticleAlpha { get; } = new("ParticleAlpha", 1f);
        public FloatParameterTrackViewModel ParticleBrightness { get; } = new("ParticleBrightness", 1f);
        public FloatParameterTrackViewModel ParticleSpinAngle { get; } = new("ParticleSpinAngle", 0f);
        public FloatParameterTrackViewModel ParticleSpinSpeed { get; } = new("ParticleSpinSpeed", 0f);
        public FloatParameterTrackViewModel ParticleScale { get; } = new("ParticleScale", 1f);
        public FloatParameterTrackViewModel ParticleStretch { get; } = new("ParticleStretch", 1f);
        public FloatParameterTrackViewModel CollisionReflect { get; } = new("CollisionReflect", 0f);
        public FloatParameterTrackViewModel CollisionSpin { get; } = new("CollisionSpin", 0f);
        public FloatParameterTrackViewModel ClipTop { get; } = new("ClipTop", 0f);
        public FloatParameterTrackViewModel ClipBottom { get; } = new("ClipBottom", 0f);
        public FloatParameterTrackViewModel ClipLeft { get; } = new("ClipLeft", 0f);
        public FloatParameterTrackViewModel ClipRight { get; } = new("ClipRight", 0f);
        public FloatParameterTrackViewModel AnimationRate { get; } = new("AnimationRate", 0f);

        public event Action<ParticleEmitterViewModel> Changed;

        public ParticleEmitterViewModel(int index, TodEmitterDefinition emitter)
        {
            Index = index;
            Loc.LanguageChanged += OnLanguageChanged;
            Bind(SystemDuration, static emitter => emitter.mSystemDuration, 0f);
            Bind(CrossFadeDuration, static emitter => emitter.mCrossFadeDuration, 0f);
            Bind(SpawnRate, static emitter => emitter.mSpawnRate, 0f);
            Bind(SpawnMinActive, static emitter => emitter.mSpawnMinActive, -1f);
            Bind(SpawnMaxActive, static emitter => emitter.mSpawnMaxActive, -1f);
            Bind(SpawnMaxLaunched, static emitter => emitter.mSpawnMaxLaunched, -1f);
            Bind(EmitterRadius, static emitter => emitter.mEmitterRadius, 0f);
            Bind(EmitterOffsetX, static emitter => emitter.mEmitterOffsetX, 0f);
            Bind(EmitterOffsetY, static emitter => emitter.mEmitterOffsetY, 0f);
            Bind(EmitterBoxX, static emitter => emitter.mEmitterBoxX, 0f);
            Bind(EmitterBoxY, static emitter => emitter.mEmitterBoxY, 0f);
            Bind(EmitterSkewX, static emitter => emitter.mEmitterSkewX, 0f);
            Bind(EmitterSkewY, static emitter => emitter.mEmitterSkewY, 0f);
            Bind(EmitterPath, static emitter => emitter.mEmitterPath, 0f);
            Bind(ParticleDuration, static emitter => emitter.mParticleDuration, 100f);
            Bind(LaunchSpeed, static emitter => emitter.mLaunchSpeed, 0f);
            Bind(LaunchAngle, static emitter => emitter.mLaunchAngle, 0f);
            Bind(SystemRed, static emitter => emitter.mSystemRed, 1f);
            Bind(SystemGreen, static emitter => emitter.mSystemGreen, 1f);
            Bind(SystemBlue, static emitter => emitter.mSystemBlue, 1f);
            Bind(SystemAlpha, static emitter => emitter.mSystemAlpha, 1f);
            Bind(SystemBrightness, static emitter => emitter.mSystemBrightness, 1f);
            Bind(ParticleRed, static emitter => emitter.mParticleRed, 1f);
            Bind(ParticleGreen, static emitter => emitter.mParticleGreen, 1f);
            Bind(ParticleBlue, static emitter => emitter.mParticleBlue, 1f);
            Bind(ParticleAlpha, static emitter => emitter.mParticleAlpha, 1f);
            Bind(ParticleBrightness, static emitter => emitter.mParticleBrightness, 1f);
            Bind(ParticleSpinAngle, static emitter => emitter.mParticleSpinAngle, 0f);
            Bind(ParticleSpinSpeed, static emitter => emitter.mParticleSpinSpeed, 0f);
            Bind(ParticleScale, static emitter => emitter.mParticleScale, 1f);
            Bind(ParticleStretch, static emitter => emitter.mParticleStretch, 1f);
            Bind(CollisionReflect, static emitter => emitter.mCollisionReflect, 0f);
            Bind(CollisionSpin, static emitter => emitter.mCollisionSpin, 0f);
            Bind(ClipTop, static emitter => emitter.mClipTop, 0f);
            Bind(ClipBottom, static emitter => emitter.mClipBottom, 0f);
            Bind(ClipLeft, static emitter => emitter.mClipLeft, 0f);
            Bind(ClipRight, static emitter => emitter.mClipRight, 0f);
            Bind(AnimationRate, static emitter => emitter.mAnimationRate, 0f);
            LoadFrom(emitter);
        }

        public void SetIndex(int index)
        {
            if (Index == index)
            {
                return;
            }

            Index = index;
            OnPropertyChanged(nameof(Index));
            OnPropertyChanged(nameof(DisplayName));
        }

        public string Name
        {
            get => _name;
            set
            {
                if (SetProperty(ref _name, value ?? string.Empty))
                {
                    OnPropertyChanged(nameof(DisplayName));
                    RaiseChanged();
                }
            }
        }

        public string ImageId
        {
            get => _imageId;
            set
            {
                if (SetProperty(ref _imageId, value ?? string.Empty))
                {
                    OnPropertyChanged(nameof(Detail));
                    RaiseChanged();
                }
            }
        }

        public int ImageRow
        {
            get => _imageRow;
            set
            {
                int clamped = Math.Max(0, value);
                if (SetProperty(ref _imageRow, clamped))
                {
                    RaiseChanged();
                }
            }
        }

        public int ImageCol
        {
            get => _imageCol;
            set
            {
                int clamped = Math.Max(0, value);
                if (SetProperty(ref _imageCol, clamped))
                {
                    RaiseChanged();
                }
            }
        }

        public int ImageFrames
        {
            get => _imageFrames;
            set
            {
                int clamped = Math.Max(1, value);
                if (SetProperty(ref _imageFrames, clamped))
                {
                    RaiseChanged();
                }
            }
        }

        public bool Animated
        {
            get => _animated;
            set
            {
                if (SetProperty(ref _animated, value))
                {
                    RaiseChanged();
                }
            }
        }

        public EmitterType EmitterType
        {
            get => _emitterType;
            set
            {
                if (SetProperty(ref _emitterType, value))
                {
                    RaiseChanged();
                }
            }
        }

        public bool RandomLaunchSpin
        {
            get => _randomLaunchSpin;
            set => SetFlagProperty(ref _randomLaunchSpin, value);
        }

        public bool AlignLaunchSpin
        {
            get => _alignLaunchSpin;
            set => SetFlagProperty(ref _alignLaunchSpin, value);
        }

        public bool AlignToPixels
        {
            get => _alignToPixels;
            set => SetFlagProperty(ref _alignToPixels, value);
        }

        public bool SystemLoops
        {
            get => _systemLoops;
            set => SetFlagProperty(ref _systemLoops, value);
        }

        public bool ParticleLoops
        {
            get => _particleLoops;
            set => SetFlagProperty(ref _particleLoops, value);
        }

        public bool ParticlesDontFollow
        {
            get => _particlesDontFollow;
            set => SetFlagProperty(ref _particlesDontFollow, value);
        }

        public bool DieIfOverloaded
        {
            get => _dieIfOverloaded;
            set => SetFlagProperty(ref _dieIfOverloaded, value);
        }

        public bool Additive
        {
            get => _additive;
            set => SetFlagProperty(ref _additive, value);
        }

        public bool Fullscreen
        {
            get => _fullscreen;
            set => SetFlagProperty(ref _fullscreen, value);
        }

        public bool SoftwareOnly
        {
            get => _softwareOnly;
            set => SetFlagProperty(ref _softwareOnly, value);
        }

        public bool HardwareOnly
        {
            get => _hardwareOnly;
            set => SetFlagProperty(ref _hardwareOnly, value);
        }

        public void LoadFrom(TodEmitterDefinition emitter)
        {
            emitter ??= new TodEmitterDefinition();
            _suppressChanged = true;
            Name = emitter.mName ?? string.Empty;
            ImageId = emitter.mImage ?? string.Empty;
            ImageRow = emitter.mImageRow;
            ImageCol = emitter.mImageCol;
            ImageFrames = emitter.mImageFrames;
            Animated = emitter.mAnimated != 0;
            EmitterType = emitter.mEmitterType;
            RandomLaunchSpin = IsFlagSet(emitter.mParticleFlags, ParticleFlags.RandomLaunchSpin);
            AlignLaunchSpin = IsFlagSet(emitter.mParticleFlags, ParticleFlags.AlignLaunchSpin);
            AlignToPixels = IsFlagSet(emitter.mParticleFlags, ParticleFlags.AlignToPixels);
            SystemLoops = IsFlagSet(emitter.mParticleFlags, ParticleFlags.SystemLoops);
            ParticleLoops = IsFlagSet(emitter.mParticleFlags, ParticleFlags.ParticleLoops);
            ParticlesDontFollow = IsFlagSet(emitter.mParticleFlags, ParticleFlags.ParticlesDontFollow);
            DieIfOverloaded = IsFlagSet(emitter.mParticleFlags, ParticleFlags.DieIfOverloaded);
            Additive = IsFlagSet(emitter.mParticleFlags, ParticleFlags.Additive);
            Fullscreen = IsFlagSet(emitter.mParticleFlags, ParticleFlags.Fullscreen);
            SoftwareOnly = IsFlagSet(emitter.mParticleFlags, ParticleFlags.SoftwareOnly);
            HardwareOnly = IsFlagSet(emitter.mParticleFlags, ParticleFlags.HardwareOnly);

            foreach (TrackBinding binding in _trackBindings)
            {
                binding.IsDirty = false;
                binding.ViewModel.LoadFrom(binding.Getter(emitter));
            }

            LoadFields(ParticleFields, emitter.mParticleFields, emitter.mParticleFieldCount, RemoveParticleField);
            LoadFields(SystemFields, emitter.mSystemFields, emitter.mSystemFieldCount, RemoveSystemField);

            _suppressChanged = false;
            OnPropertyChanged(nameof(DisplayName));
            OnPropertyChanged(nameof(Detail));
        }

        public void ApplyTo(TodEmitterDefinition emitter)
        {
            if (emitter is null)
            {
                return;
            }

            emitter.mName = Name ?? string.Empty;
            emitter.mImage = string.IsNullOrWhiteSpace(ImageId) ? null : ImageId.Trim();
            emitter.mImageRow = Math.Max(0, ImageRow);
            emitter.mImageCol = Math.Max(0, ImageCol);
            emitter.mImageFrames = Math.Max(1, ImageFrames);
            emitter.mAnimated = Animated ? 1 : 0;
            emitter.mEmitterType = EmitterType;

            int flags = emitter.mParticleFlags;
            SetFlag(ref flags, ParticleFlags.RandomLaunchSpin, RandomLaunchSpin);
            SetFlag(ref flags, ParticleFlags.AlignLaunchSpin, AlignLaunchSpin);
            SetFlag(ref flags, ParticleFlags.AlignToPixels, AlignToPixels);
            SetFlag(ref flags, ParticleFlags.SystemLoops, SystemLoops);
            SetFlag(ref flags, ParticleFlags.ParticleLoops, ParticleLoops);
            SetFlag(ref flags, ParticleFlags.ParticlesDontFollow, ParticlesDontFollow);
            SetFlag(ref flags, ParticleFlags.DieIfOverloaded, DieIfOverloaded);
            SetFlag(ref flags, ParticleFlags.Additive, Additive);
            SetFlag(ref flags, ParticleFlags.Fullscreen, Fullscreen);
            SetFlag(ref flags, ParticleFlags.SoftwareOnly, SoftwareOnly);
            SetFlag(ref flags, ParticleFlags.HardwareOnly, HardwareOnly);
            emitter.mParticleFlags = flags;

            foreach (TrackBinding binding in _trackBindings)
            {
                if (!binding.IsDirty)
                {
                    continue;
                }

                FloatParameterTrack track = binding.Getter(emitter);
                binding.ViewModel.ApplyTo(track);
                NormalizeEditedTrack(track, binding.DefaultValue);
            }

            emitter.mParticleFields = BuildFields(ParticleFields);
            emitter.mParticleFieldCount = emitter.mParticleFields?.Length ?? 0;
            emitter.mSystemFields = BuildFields(SystemFields);
            emitter.mSystemFieldCount = emitter.mSystemFields?.Length ?? 0;
        }

        public void Dispose()
        {
            Loc.LanguageChanged -= OnLanguageChanged;
            foreach (TrackBinding binding in _trackBindings)
            {
                binding.ViewModel.Changed -= OnTrackChanged;
            }

            DisposeFields(ParticleFields);
            DisposeFields(SystemFields);
        }

        private static LocalizationManager Loc => LocalizationManager.Instance;

        private void OnLanguageChanged(object sender, EventArgs e)
        {
            OnPropertyChanged(nameof(DisplayName));
            OnPropertyChanged(nameof(Detail));
        }

        [RelayCommand]
        private void AddParticleField()
        {
            AddField(ParticleFields, RemoveParticleField);
        }

        [RelayCommand]
        private void AddSystemField()
        {
            AddField(SystemFields, RemoveSystemField);
        }

        private void Bind(FloatParameterTrackViewModel viewModel, Func<TodEmitterDefinition, FloatParameterTrack> getter, float defaultValue)
        {
            _trackBindings.Add(new TrackBinding
            {
                ViewModel = viewModel,
                Getter = getter,
                DefaultValue = defaultValue
            });
            viewModel.Changed += OnTrackChanged;
        }

        private void OnTrackChanged(FloatParameterTrackViewModel track)
        {
            TrackBinding binding = _trackBindings.Find(item => ReferenceEquals(item.ViewModel, track));
            if (binding is not null)
            {
                binding.IsDirty = true;
            }

            RaiseChanged();
        }

        private void OnFieldChanged(ParticleFieldViewModel field)
        {
            RaiseChanged();
        }

        private void AddField(ObservableCollection<ParticleFieldViewModel> fields, Action<ParticleFieldViewModel> remove)
        {
            ParticleFieldViewModel field = new(fields.Count, new ParticleField(), remove);
            field.Changed += OnFieldChanged;
            fields.Add(field);
            RaiseChanged();
        }

        private void RemoveParticleField(ParticleFieldViewModel field)
        {
            RemoveField(ParticleFields, field);
        }

        private void RemoveSystemField(ParticleFieldViewModel field)
        {
            RemoveField(SystemFields, field);
        }

        private void RemoveField(ObservableCollection<ParticleFieldViewModel> fields, ParticleFieldViewModel field)
        {
            if (field is null || !fields.Remove(field))
            {
                return;
            }

            field.Changed -= OnFieldChanged;
            field.Dispose();
            ReindexFields(fields);
            RaiseChanged();
        }

        private void LoadFields(
            ObservableCollection<ParticleFieldViewModel> target,
            ParticleField[] source,
            int sourceCount,
            Action<ParticleFieldViewModel> remove)
        {
            DisposeFields(target);
            target.Clear();

            int count = source is null ? 0 : Math.Min(source.Length, sourceCount);
            for (int i = 0; i < count; i++)
            {
                ParticleFieldViewModel field = new(i, source[i], remove);
                field.Changed += OnFieldChanged;
                target.Add(field);
            }
        }

        private static ParticleField[] BuildFields(ObservableCollection<ParticleFieldViewModel> fields)
        {
            if (fields.Count == 0)
            {
                return null;
            }

            ParticleField[] result = new ParticleField[fields.Count];
            for (int i = 0; i < fields.Count; i++)
            {
                result[i] = fields[i].ToField();
            }

            return result;
        }

        private void DisposeFields(ObservableCollection<ParticleFieldViewModel> fields)
        {
            foreach (ParticleFieldViewModel field in fields)
            {
                field.Changed -= OnFieldChanged;
                field.Dispose();
            }
        }

        private static void ReindexFields(ObservableCollection<ParticleFieldViewModel> fields)
        {
            for (int i = 0; i < fields.Count; i++)
            {
                fields[i].SetIndex(i);
            }
        }

        private void SetFlagProperty(ref bool field, bool value)
        {
            if (SetProperty(ref field, value))
            {
                RaiseChanged();
            }
        }

        private void RaiseChanged()
        {
            if (!_suppressChanged)
            {
                Changed?.Invoke(this);
            }
        }

        private static bool IsFlagSet(int flags, ParticleFlags flag)
        {
            return (flags & (1 << (int)flag)) != 0;
        }

        private static void SetFlag(ref int flags, ParticleFlags flag, bool value)
        {
            int mask = 1 << (int)flag;
            if (value)
            {
                flags |= mask;
            }
            else
            {
                flags &= ~mask;
            }
        }

        private static void NormalizeEditedTrack(FloatParameterTrack track, float defaultValue)
        {
            if (track?.mNodes is null || track.mCountNodes != 1 || track.mNodes.Length == 0)
            {
                return;
            }

            FloatParameterTrackNode node = track.mNodes[0];
            bool isDefaultConstant = node.mTime == 0f &&
                node.mLowValue == node.mHighValue &&
                node.mCurveType == TodCurves.Constant &&
                node.mDistribution == TodCurves.Linear &&
                Math.Abs(node.mLowValue - defaultValue) < 0.0005f;
            if (isDefaultConstant)
            {
                track.mNodes = null;
                track.mCountNodes = 0;
                return;
            }

            if (node.mCurveType == TodCurves.Constant &&
                node.mLowValue == node.mHighValue &&
                Math.Abs(node.mLowValue - defaultValue) >= 0.0005f)
            {
                node.mCurveType = TodCurves.Linear;
            }
        }
    }
}
