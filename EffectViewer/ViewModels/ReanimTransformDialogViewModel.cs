using CommunityToolkit.Mvvm.Input;

namespace EffectViewer.ViewModels
{
    public sealed partial class ReanimTransformDialogViewModel : ViewModelBase
    {
        private readonly System.Action _apply;
        private readonly System.Action _close;
        private string _trackName = string.Empty;
        private string _imageId = string.Empty;
        private string _fontId = string.Empty;
        private string _text = string.Empty;
        private double _x;
        private double _y;
        private double _scaleX = 1d;
        private double _scaleY = 1d;
        private double _skewX;
        private double _skewY;
        private double _frame;
        private double _alpha = 1d;

        public string TrackName
        {
            get => _trackName;
            set
            {
                if (SetProperty(ref _trackName, value ?? string.Empty))
                {
                    _apply?.Invoke();
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
                    _apply?.Invoke();
                }
            }
        }

        public string FontId
        {
            get => _fontId;
            set
            {
                if (SetProperty(ref _fontId, value ?? string.Empty))
                {
                    _apply?.Invoke();
                }
            }
        }

        public string Text
        {
            get => _text;
            set
            {
                if (SetProperty(ref _text, value ?? string.Empty))
                {
                    _apply?.Invoke();
                }
            }
        }

        public double X
        {
            get => _x;
            set
            {
                if (SetProperty(ref _x, Round(value)))
                {
                    _apply?.Invoke();
                }
            }
        }

        public double Y
        {
            get => _y;
            set
            {
                if (SetProperty(ref _y, Round(value)))
                {
                    _apply?.Invoke();
                }
            }
        }

        public double ScaleX
        {
            get => _scaleX;
            set
            {
                if (SetProperty(ref _scaleX, Round(value)))
                {
                    _apply?.Invoke();
                }
            }
        }

        public double ScaleY
        {
            get => _scaleY;
            set
            {
                if (SetProperty(ref _scaleY, Round(value)))
                {
                    _apply?.Invoke();
                }
            }
        }

        public double SkewX
        {
            get => _skewX;
            set
            {
                if (SetProperty(ref _skewX, Round(value)))
                {
                    _apply?.Invoke();
                }
            }
        }

        public double SkewY
        {
            get => _skewY;
            set
            {
                if (SetProperty(ref _skewY, Round(value)))
                {
                    _apply?.Invoke();
                }
            }
        }

        public double Frame
        {
            get => _frame;
            set
            {
                if (SetProperty(ref _frame, NormalizeFrame(value)))
                {
                    _apply?.Invoke();
                }
            }
        }

        public double Alpha
        {
            get => _alpha;
            set
            {
                if (SetProperty(ref _alpha, Round(System.Math.Clamp(value, 0d, 1d))))
                {
                    _apply?.Invoke();
                }
            }
        }

        public ReanimTransformDialogViewModel(System.Action apply, System.Action close)
        {
            _apply = apply;
            _close = close;
        }

        public void Load(
            string trackName,
            string imageId,
            string fontId,
            string text,
            double x,
            double y,
            double scaleX,
            double scaleY,
            double skewX,
            double skewY,
            double frame,
            double alpha)
        {
            SetProperty(ref _trackName, trackName ?? string.Empty, nameof(TrackName));
            SetProperty(ref _imageId, imageId ?? string.Empty, nameof(ImageId));
            SetProperty(ref _fontId, fontId ?? string.Empty, nameof(FontId));
            SetProperty(ref _text, text ?? string.Empty, nameof(Text));
            SetProperty(ref _x, Round(x), nameof(X));
            SetProperty(ref _y, Round(y), nameof(Y));
            SetProperty(ref _scaleX, Round(scaleX), nameof(ScaleX));
            SetProperty(ref _scaleY, Round(scaleY), nameof(ScaleY));
            SetProperty(ref _skewX, Round(skewX), nameof(SkewX));
            SetProperty(ref _skewY, Round(skewY), nameof(SkewY));
            SetProperty(ref _frame, NormalizeFrame(frame), nameof(Frame));
            SetProperty(ref _alpha, Round(alpha), nameof(Alpha));
        }

        [RelayCommand]
        private void ResetScale()
        {
            ScaleX = 1d;
            ScaleY = 1d;
        }

        [RelayCommand]
        private void ResetSkew()
        {
            SkewX = 0d;
            SkewY = 0d;
        }

        [RelayCommand]
        private void Close()
        {
            _close?.Invoke();
        }

        private static double Round(double value)
        {
            double rounded = System.Math.Round(value, 3, System.MidpointRounding.AwayFromZero);
            return rounded == -0d ? 0d : rounded;
        }

        private static double NormalizeFrame(double value)
        {
            double rounded = Round(value);
            return rounded < 0d ? -1d : rounded;
        }
    }
}
