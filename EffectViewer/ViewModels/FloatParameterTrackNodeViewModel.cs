using System;
using CommunityToolkit.Mvvm.Input;
using EffectViewer.TodLib.Common;

namespace EffectViewer.ViewModels
{
    public sealed partial class FloatParameterTrackNodeViewModel : ViewModelBase
    {
        private readonly Action<FloatParameterTrackNodeViewModel> _remove;
        private double _timePercent;
        private double _lowValue;
        private double _highValue;
        private TodCurves _curveType;
        private TodCurves _distribution;

        public TodCurves[] CurveOptions { get; } = Enum.GetValues<TodCurves>();

        public double TimePercent
        {
            get => _timePercent;
            set
            {
                double clamped = Math.Clamp(value, 0d, 100d);
                if (SetProperty(ref _timePercent, clamped))
                {
                    Changed?.Invoke(this);
                }
            }
        }

        public double LowValue
        {
            get => _lowValue;
            set
            {
                if (SetProperty(ref _lowValue, value))
                {
                    Changed?.Invoke(this);
                }
            }
        }

        public double HighValue
        {
            get => _highValue;
            set
            {
                if (SetProperty(ref _highValue, value))
                {
                    Changed?.Invoke(this);
                }
            }
        }

        public TodCurves CurveType
        {
            get => _curveType;
            set
            {
                if (SetProperty(ref _curveType, value))
                {
                    Changed?.Invoke(this);
                }
            }
        }

        public TodCurves Distribution
        {
            get => _distribution;
            set
            {
                if (SetProperty(ref _distribution, value))
                {
                    Changed?.Invoke(this);
                }
            }
        }

        public event Action<FloatParameterTrackNodeViewModel> Changed;

        public FloatParameterTrackNodeViewModel(Action<FloatParameterTrackNodeViewModel> remove)
        {
            _remove = remove;
        }

        [RelayCommand]
        private void Remove()
        {
            _remove?.Invoke(this);
        }
    }
}
