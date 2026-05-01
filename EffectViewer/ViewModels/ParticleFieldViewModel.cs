using System;
using CommunityToolkit.Mvvm.Input;
using EffectViewer.TodLib.Common;
using EffectViewer.TodLib.Particle;

namespace EffectViewer.ViewModels
{
    public sealed partial class ParticleFieldViewModel : ViewModelBase, IDisposable
    {
        private readonly Action<ParticleFieldViewModel> _remove;
        private bool _suppressChanged;
        private ParticleFieldType _fieldType;

        public int Index { get; private set; }
        public string DisplayName => $"{Index + 1}. {FieldType}";
        public ParticleFieldType[] FieldTypeOptions { get; } = Enum.GetValues<ParticleFieldType>();
        public FloatParameterTrackViewModel X { get; } = new("x", 0f);
        public FloatParameterTrackViewModel Y { get; } = new("y", 0f);

        public event Action<ParticleFieldViewModel> Changed;

        public ParticleFieldViewModel(int index, ParticleField field, Action<ParticleFieldViewModel> remove)
        {
            Index = index;
            _remove = remove;
            X.Changed += OnTrackChanged;
            Y.Changed += OnTrackChanged;
            LoadFrom(field);
        }

        public ParticleFieldType FieldType
        {
            get => _fieldType;
            set
            {
                if (SetProperty(ref _fieldType, value))
                {
                    OnPropertyChanged(nameof(DisplayName));
                    RaiseChanged();
                }
            }
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

        public void LoadFrom(ParticleField field)
        {
            field ??= new ParticleField();
            _suppressChanged = true;
            FieldType = field.mFieldType;
            X.LoadFrom(field.mX);
            Y.LoadFrom(field.mY);
            _suppressChanged = false;
            OnPropertyChanged(nameof(DisplayName));
        }

        public ParticleField ToField()
        {
            ParticleField field = new()
            {
                mFieldType = FieldType
            };
            X.ApplyTo(field.mX);
            Y.ApplyTo(field.mY);
            NormalizeEditedTrack(field.mX, 0f);
            NormalizeEditedTrack(field.mY, 0f);
            return field;
        }

        public void Dispose()
        {
            X.Changed -= OnTrackChanged;
            Y.Changed -= OnTrackChanged;
        }

        [RelayCommand]
        private void Remove()
        {
            _remove?.Invoke(this);
        }

        private void OnTrackChanged(FloatParameterTrackViewModel track)
        {
            RaiseChanged();
        }

        private void RaiseChanged()
        {
            if (!_suppressChanged)
            {
                Changed?.Invoke(this);
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
