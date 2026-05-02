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
        private string _xText = "0";
        private string _yText = "0";
        private string _scaleXText = "1";
        private string _scaleYText = "1";
        private string _skewXText = "0";
        private string _skewYText = "0";
        private string _frameText = "0";
        private string _alphaText = "1";

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
                SetNumber(ref _x, Round(value), nameof(X), ref _xText, nameof(XText));
            }
        }

        public double Y
        {
            get => _y;
            set
            {
                SetNumber(ref _y, Round(value), nameof(Y), ref _yText, nameof(YText));
            }
        }

        public double ScaleX
        {
            get => _scaleX;
            set
            {
                SetNumber(ref _scaleX, Round(value), nameof(ScaleX), ref _scaleXText, nameof(ScaleXText));
            }
        }

        public double ScaleY
        {
            get => _scaleY;
            set
            {
                SetNumber(ref _scaleY, Round(value), nameof(ScaleY), ref _scaleYText, nameof(ScaleYText));
            }
        }

        public double SkewX
        {
            get => _skewX;
            set
            {
                SetNumber(ref _skewX, Round(value), nameof(SkewX), ref _skewXText, nameof(SkewXText));
            }
        }

        public double SkewY
        {
            get => _skewY;
            set
            {
                SetNumber(ref _skewY, Round(value), nameof(SkewY), ref _skewYText, nameof(SkewYText));
            }
        }

        public double Frame
        {
            get => _frame;
            set
            {
                SetNumber(ref _frame, NormalizeFrame(value), nameof(Frame), ref _frameText, nameof(FrameText));
            }
        }

        public bool Visible
        {
            get => _frame >= 0d;
            set
            {
                Frame = value ? 0d : -1d;
            }
        }

        public double Alpha
        {
            get => _alpha;
            set
            {
                SetNumber(ref _alpha, Round(System.Math.Clamp(value, 0d, 1d)), nameof(Alpha), ref _alphaText, nameof(AlphaText));
            }
        }

        public string XText
        {
            get => _xText;
            set
            {
                SetNumberText(ref _xText, value, nameof(XText), nameof(X));
            }
        }

        public string YText
        {
            get => _yText;
            set
            {
                SetNumberText(ref _yText, value, nameof(YText), nameof(Y));
            }
        }

        public string ScaleXText
        {
            get => _scaleXText;
            set
            {
                SetNumberText(ref _scaleXText, value, nameof(ScaleXText), nameof(ScaleX));
            }
        }

        public string ScaleYText
        {
            get => _scaleYText;
            set
            {
                SetNumberText(ref _scaleYText, value, nameof(ScaleYText), nameof(ScaleY));
            }
        }

        public string SkewXText
        {
            get => _skewXText;
            set
            {
                SetNumberText(ref _skewXText, value, nameof(SkewXText), nameof(SkewX));
            }
        }

        public string SkewYText
        {
            get => _skewYText;
            set
            {
                SetNumberText(ref _skewYText, value, nameof(SkewYText), nameof(SkewY));
            }
        }

        public string FrameText
        {
            get => _frameText;
            set
            {
                SetNumberText(ref _frameText, value, nameof(FrameText), nameof(Frame));
            }
        }

        public string AlphaText
        {
            get => _alphaText;
            set
            {
                SetNumberText(ref _alphaText, value, nameof(AlphaText), nameof(Alpha));
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
            LoadNumber(ref _x, Round(x), nameof(X), ref _xText, nameof(XText));
            LoadNumber(ref _y, Round(y), nameof(Y), ref _yText, nameof(YText));
            LoadNumber(ref _scaleX, Round(scaleX), nameof(ScaleX), ref _scaleXText, nameof(ScaleXText));
            LoadNumber(ref _scaleY, Round(scaleY), nameof(ScaleY), ref _scaleYText, nameof(ScaleYText));
            LoadNumber(ref _skewX, Round(skewX), nameof(SkewX), ref _skewXText, nameof(SkewXText));
            LoadNumber(ref _skewY, Round(skewY), nameof(SkewY), ref _skewYText, nameof(SkewYText));
            LoadNumber(ref _frame, NormalizeFrame(frame), nameof(Frame), ref _frameText, nameof(FrameText));
            OnPropertyChanged(nameof(Visible));
            LoadNumber(ref _alpha, Round(alpha), nameof(Alpha), ref _alphaText, nameof(AlphaText));
        }

        public void RestoreNumberText(string numberName)
        {
            switch (numberName)
            {
                case nameof(X):
                    SetProperty(ref _xText, FormatNumber(_x), nameof(XText));
                    break;
                case nameof(Y):
                    SetProperty(ref _yText, FormatNumber(_y), nameof(YText));
                    break;
                case nameof(ScaleX):
                    SetProperty(ref _scaleXText, FormatNumber(_scaleX), nameof(ScaleXText));
                    break;
                case nameof(ScaleY):
                    SetProperty(ref _scaleYText, FormatNumber(_scaleY), nameof(ScaleYText));
                    break;
                case nameof(SkewX):
                    SetProperty(ref _skewXText, FormatNumber(_skewX), nameof(SkewXText));
                    break;
                case nameof(SkewY):
                    SetProperty(ref _skewYText, FormatNumber(_skewY), nameof(SkewYText));
                    break;
                case nameof(Frame):
                    SetProperty(ref _frameText, FormatNumber(_frame), nameof(FrameText));
                    break;
                case nameof(Alpha):
                    SetProperty(ref _alphaText, FormatNumber(_alpha), nameof(AlphaText));
                    break;
            }
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

        private void SetNumber(
            ref double field,
            double value,
            string numberPropertyName,
            ref string textField,
            string textPropertyName)
        {
            bool changed = SetProperty(ref field, value, numberPropertyName);
            SetProperty(ref textField, FormatNumber(value), textPropertyName);
            if (changed)
            {
                RaiseNumberSideEffects(numberPropertyName);
                _apply?.Invoke();
            }
        }

        private void LoadNumber(
            ref double numberField,
            double value,
            string numberPropertyName,
            ref string textField,
            string textPropertyName)
        {
            SetProperty(ref numberField, value, numberPropertyName);
            SetProperty(ref textField, FormatNumber(value), textPropertyName);
        }

        private void SetNumberText(
            ref string textField,
            string value,
            string textPropertyName,
            string numberPropertyName)
        {
            if (SetProperty(ref textField, value ?? string.Empty, textPropertyName))
            {
                TryApplyNumberText(numberPropertyName, textField);
            }
        }

        private void TryApplyNumberText(string numberPropertyName, string text)
        {
            if (!TryParseNumber(text, out double value))
            {
                return;
            }

            switch (numberPropertyName)
            {
                case nameof(X):
                    TrySetParsedNumber(ref _x, value, -10000d, 10000d, nameof(X));
                    break;
                case nameof(Y):
                    TrySetParsedNumber(ref _y, value, -10000d, 10000d, nameof(Y));
                    break;
                case nameof(ScaleX):
                    TrySetParsedNumber(ref _scaleX, value, -10000d, 10000d, nameof(ScaleX));
                    break;
                case nameof(ScaleY):
                    TrySetParsedNumber(ref _scaleY, value, -10000d, 10000d, nameof(ScaleY));
                    break;
                case nameof(SkewX):
                    TrySetParsedNumber(ref _skewX, value, -10000d, 10000d, nameof(SkewX));
                    break;
                case nameof(SkewY):
                    TrySetParsedNumber(ref _skewY, value, -10000d, 10000d, nameof(SkewY));
                    break;
                case nameof(Frame):
                    TrySetParsedNumber(ref _frame, value, -1d, 10000d, nameof(Frame), normalizeFrame: true);
                    break;
                case nameof(Alpha):
                    TrySetParsedNumber(ref _alpha, value, -10000d, 10000d, nameof(Alpha));
                    break;
            }
        }

        private void TrySetParsedNumber(
            ref double field,
            double value,
            double minimum,
            double maximum,
            string numberPropertyName,
            bool normalizeFrame = false)
        {
            if (value < minimum || value > maximum)
            {
                return;
            }

            double normalized = normalizeFrame ? NormalizeFrame(value) : Round(value);
            if (SetProperty(ref field, normalized, numberPropertyName))
            {
                RaiseNumberSideEffects(numberPropertyName);
                _apply?.Invoke();
            }
        }

        private void RaiseNumberSideEffects(string numberPropertyName)
        {
            if (numberPropertyName == nameof(Frame))
            {
                OnPropertyChanged(nameof(Visible));
            }
        }

        private static bool TryParseNumber(string text, out double value)
        {
            text = text?.Trim();
            if (string.IsNullOrEmpty(text))
            {
                value = 0d;
                return false;
            }

            if (!double.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.CurrentCulture, out value) &&
                !double.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out value))
            {
                return false;
            }

            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        private static string FormatNumber(double value)
        {
            return Round(value).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
