using System;
using CommunityToolkit.Mvvm.Input;
using EffectViewer.TodLib.Common;

namespace EffectViewer.ViewModels
{
    public sealed partial class FloatParameterTrackNodeViewModel : ViewModelBase
    {
        private readonly Action<FloatParameterTrackNodeViewModel> _remove;
        private readonly Action<FloatParameterTrackNodeViewModel> _copy;
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
                double clamped = RoundToThreeDecimals(Math.Clamp(value, 0d, 100d));
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
                double rounded = RoundToThreeDecimals(value);
                if (SetProperty(ref _lowValue, rounded))
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
                double rounded = RoundToThreeDecimals(value);
                if (SetProperty(ref _highValue, rounded))
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

        public FloatParameterTrackNodeViewModel(
            Action<FloatParameterTrackNodeViewModel> remove,
            Action<FloatParameterTrackNodeViewModel> copy)
        {
            _remove = remove;
            _copy = copy;
        }

        [RelayCommand]
        private void Remove()
        {
            _remove?.Invoke(this);
        }

        [RelayCommand]
        private void Copy()
        {
            _copy?.Invoke(this);
        }

        private static double RoundToThreeDecimals(double value)
        {
            double rounded = Math.Round(value, 3, MidpointRounding.AwayFromZero);
            return rounded == -0d ? 0d : rounded;
        }
    }
}
