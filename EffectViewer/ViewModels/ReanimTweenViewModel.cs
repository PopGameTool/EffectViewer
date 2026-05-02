using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using EffectViewer.Assets;

namespace EffectViewer.ViewModels
{
    public sealed class ReanimTweenViewModel : ViewModelBase
    {
        private readonly Func<int, string> _trackNameResolver;
        private readonly Action<ReanimTweenViewModel> _changed;
        private string _trackNumberText;
        private string _startFrameNumberText;
        private string _endFrameNumberText;

        public ReanimTween Model { get; }
        public int Index { get; private set; }
        public int TrackCount { get; }
        public int FrameCount { get; }

        public string DisplayName => $"{Index + 1}. {TrackName} [{StartFrameNumber:0}-{EndFrameNumber:0}]";
        public string Detail => string.Join(", ", NormalizeProperties(Model.Properties).Select(ToDisplayPropertyName));
        public string TrackName => _trackNameResolver?.Invoke(Model.TrackIndex) ?? Model.TrackName ?? string.Empty;
        public double TrackNumber
        {
            get => Model.TrackIndex + 1;
            set => SetTrackNumber(value);
        }

        public string TrackNumberText
        {
            get => _trackNumberText;
            set => SetNumberText(ref _trackNumberText, value, nameof(TrackNumberText), nameof(TrackNumber));
        }

        public double StartFrameNumber
        {
            get => Model.StartFrame + 1;
            set => SetStartFrameNumber(value);
        }

        public string StartFrameNumberText
        {
            get => _startFrameNumberText;
            set => SetNumberText(ref _startFrameNumberText, value, nameof(StartFrameNumberText), nameof(StartFrameNumber));
        }

        public double EndFrameNumber
        {
            get => Model.EndFrame + 1;
            set => SetEndFrameNumber(value);
        }

        public string EndFrameNumberText
        {
            get => _endFrameNumberText;
            set => SetNumberText(ref _endFrameNumberText, value, nameof(EndFrameNumberText), nameof(EndFrameNumber));
        }

        public double StartFrameMaximum => Math.Max(1, FrameCount - 2);
        public double EndFrameMinimum => Math.Min(FrameCount, Model.StartFrame + 3);

        public ReanimTweenViewModel(
            int index,
            ReanimTween model,
            int trackCount,
            int frameCount,
            Func<int, string> trackNameResolver,
            Action<ReanimTweenViewModel> changed)
        {
            Index = index;
            Model = model ?? new ReanimTween();
            TrackCount = Math.Max(1, trackCount);
            FrameCount = Math.Max(3, frameCount);
            _trackNameResolver = trackNameResolver;
            _changed = changed;
            NormalizeModel();
            RestoreAllNumberText();
        }

        public void SetIndex(int index)
        {
            if (Index == index)
            {
                return;
            }

            Index = index;
            RaiseDisplayPropertiesChanged();
        }

        public void SetTrackNumber(double value)
        {
            int trackIndex = ClampRounded(value, 1, TrackCount) - 1;
            if (Model.TrackIndex == trackIndex)
            {
                return;
            }

            Model.TrackIndex = trackIndex;
            Model.TrackName = TrackName;
            OnPropertyChanged(nameof(TrackNumber));
            RestoreNumberText(nameof(TrackNumber));
            RaiseDisplayPropertiesChanged();
            RaiseChanged();
        }

        public void SetStartFrameNumber(double value)
        {
            int maxStart = Math.Max(0, FrameCount - 3);
            int startFrame = ClampRounded(value, 1, maxStart + 1) - 1;
            if (Model.StartFrame == startFrame)
            {
                return;
            }

            Model.StartFrame = startFrame;
            if (Model.EndFrame <= Model.StartFrame + 1)
            {
                Model.EndFrame = Math.Min(FrameCount - 1, Model.StartFrame + 2);
            }

            RaiseFramePropertiesChanged();
            RestoreFrameNumberText();
            RaiseChanged();
        }

        public void SetEndFrameNumber(double value)
        {
            int minEnd = Math.Min(FrameCount - 1, Model.StartFrame + 2);
            int endFrame = ClampRounded(value, minEnd + 1, FrameCount) - 1;
            if (Model.EndFrame == endFrame)
            {
                return;
            }

            Model.EndFrame = endFrame;
            RaiseFramePropertiesChanged();
            RestoreNumberText(nameof(EndFrameNumber));
            RaiseChanged();
        }

        public void RestoreNumberText(string numberName)
        {
            switch (numberName)
            {
                case nameof(TrackNumber):
                    SetProperty(ref _trackNumberText, FormatNumber(TrackNumber), nameof(TrackNumberText));
                    break;
                case nameof(StartFrameNumber):
                    SetProperty(ref _startFrameNumberText, FormatNumber(StartFrameNumber), nameof(StartFrameNumberText));
                    break;
                case nameof(EndFrameNumber):
                    SetProperty(ref _endFrameNumberText, FormatNumber(EndFrameNumber), nameof(EndFrameNumberText));
                    break;
            }
        }

        public void RefreshTrackName()
        {
            string trackName = TrackName;
            if (Model.TrackName != trackName)
            {
                Model.TrackName = trackName;
            }

            OnPropertyChanged(nameof(TrackName));
            RaiseDisplayPropertiesChanged();
        }

        private void NormalizeModel()
        {
            Model.TrackIndex = Math.Clamp(Model.TrackIndex, 0, TrackCount - 1);
            Model.TrackName = TrackName;
            Model.StartFrame = Math.Clamp(Model.StartFrame, 0, FrameCount - 3);
            Model.EndFrame = Math.Clamp(Model.EndFrame, Model.StartFrame + 2, FrameCount - 1);
            Model.Properties = NormalizeProperties(Model.Properties);
        }

        private void RaiseFramePropertiesChanged()
        {
            OnPropertyChanged(nameof(StartFrameNumber));
            OnPropertyChanged(nameof(EndFrameNumber));
            OnPropertyChanged(nameof(StartFrameMaximum));
            OnPropertyChanged(nameof(EndFrameMinimum));
            RaiseDisplayPropertiesChanged();
        }

        private void RaiseDisplayPropertiesChanged()
        {
            OnPropertyChanged(nameof(Index));
            OnPropertyChanged(nameof(DisplayName));
            OnPropertyChanged(nameof(Detail));
            OnPropertyChanged(nameof(TrackName));
        }

        private void RaiseChanged()
        {
            _changed?.Invoke(this);
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
            if (!TryParseRoundedNumber(text, out int value))
            {
                return;
            }

            switch (numberPropertyName)
            {
                case nameof(TrackNumber):
                    if (value >= 1 && value <= TrackCount)
                    {
                        SetTrackNumber(value);
                    }
                    break;
                case nameof(StartFrameNumber):
                    if (value >= 1 && value <= StartFrameMaximum)
                    {
                        SetStartFrameNumber(value);
                    }
                    break;
                case nameof(EndFrameNumber):
                    if (value >= EndFrameMinimum && value <= FrameCount)
                    {
                        SetEndFrameNumber(value);
                    }
                    break;
            }
        }

        private void RestoreAllNumberText()
        {
            RestoreNumberText(nameof(TrackNumber));
            RestoreFrameNumberText();
        }

        private void RestoreFrameNumberText()
        {
            RestoreNumberText(nameof(StartFrameNumber));
            RestoreNumberText(nameof(EndFrameNumber));
        }

        private static int ClampRounded(double value, int min, int max)
        {
            int rounded = (int)Math.Round(value, MidpointRounding.AwayFromZero);
            return Math.Clamp(rounded, Math.Min(min, max), Math.Max(min, max));
        }

        private static bool TryParseRoundedNumber(string text, out int value)
        {
            text = text?.Trim();
            if (string.IsNullOrEmpty(text) ||
                !double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out double number) &&
                !double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out number) ||
                double.IsNaN(number) ||
                double.IsInfinity(number))
            {
                value = 0;
                return false;
            }

            value = (int)Math.Round(number, MidpointRounding.AwayFromZero);
            return true;
        }

        private static string FormatNumber(double value)
        {
            return Math.Round(value, MidpointRounding.AwayFromZero).ToString("0", CultureInfo.InvariantCulture);
        }

        private static List<string> NormalizeProperties(IEnumerable<string> properties)
        {
            return ReanimTween.CreateTweenedProperties();
        }

        private static string ToDisplayPropertyName(string propertyName)
        {
            return propertyName switch
            {
                "x" => "X",
                "y" => "Y",
                "skewX" => "Skew X",
                "skewY" => "Skew Y",
                "scaleX" => "Scale X",
                "scaleY" => "Scale Y",
                "alpha" => "Alpha",
                _ => propertyName
            };
        }
    }
}
