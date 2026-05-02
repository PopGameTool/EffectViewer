using System;
using System.Collections.Generic;
using System.Linq;
using EffectViewer.Assets;

namespace EffectViewer.ViewModels
{
    public sealed class ReanimTweenViewModel : ViewModelBase
    {
        private readonly Func<int, string> _trackNameResolver;
        private readonly Action<ReanimTweenViewModel> _changed;

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

        public double StartFrameNumber
        {
            get => Model.StartFrame + 1;
            set => SetStartFrameNumber(value);
        }

        public double EndFrameNumber
        {
            get => Model.EndFrame + 1;
            set => SetEndFrameNumber(value);
        }
        public double StartFrameMaximum => Math.Max(1, FrameCount - 2);
        public double EndFrameMinimum => Math.Min(FrameCount, Model.StartFrame + 3);

        public bool TweenX
        {
            get => HasProperty("x");
            set => SetTweenProperty("x", value);
        }

        public bool TweenY
        {
            get => HasProperty("y");
            set => SetTweenProperty("y", value);
        }

        public bool TweenSkewX
        {
            get => HasProperty("skewX");
            set => SetTweenProperty("skewX", value);
        }

        public bool TweenSkewY
        {
            get => HasProperty("skewY");
            set => SetTweenProperty("skewY", value);
        }

        public bool TweenScaleX
        {
            get => HasProperty("scaleX");
            set => SetTweenProperty("scaleX", value);
        }

        public bool TweenScaleY
        {
            get => HasProperty("scaleY");
            set => SetTweenProperty("scaleY", value);
        }

        public bool TweenAlpha
        {
            get => HasProperty("alpha");
            set => SetTweenProperty("alpha", value);
        }

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
            RaiseChanged();
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

        private bool HasProperty(string propertyName)
        {
            return Model.Properties is not null &&
                Model.Properties.Any(property => string.Equals(property, propertyName, StringComparison.OrdinalIgnoreCase));
        }

        private void SetTweenProperty(string propertyName, bool enabled)
        {
            Model.Properties ??= [];
            bool hasProperty = HasProperty(propertyName);
            if (enabled == hasProperty)
            {
                return;
            }

            if (enabled)
            {
                Model.Properties.Add(propertyName);
            }
            else
            {
                List<string> normalized = NormalizeProperties(Model.Properties);
                if (normalized.Count <= 1)
                {
                    OnPropertyChanged(GetBooleanPropertyName(propertyName));
                    return;
                }

                Model.Properties.RemoveAll(property => string.Equals(property, propertyName, StringComparison.OrdinalIgnoreCase));
            }

            Model.Properties = NormalizeProperties(Model.Properties);
            OnPropertyChanged(GetBooleanPropertyName(propertyName));
            OnPropertyChanged(nameof(Detail));
            RaiseDisplayPropertiesChanged();
            RaiseChanged();
        }

        private void NormalizeModel()
        {
            Model.TrackIndex = Math.Clamp(Model.TrackIndex, 0, TrackCount - 1);
            Model.TrackName = TrackName;
            Model.StartFrame = Math.Clamp(Model.StartFrame, 0, FrameCount - 3);
            Model.EndFrame = Math.Clamp(Model.EndFrame, Model.StartFrame + 2, FrameCount - 1);
            Model.Properties = NormalizeProperties(Model.Properties);
            if (Model.Properties.Count == 0)
            {
                Model.Properties.Add("x");
            }
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

        private static int ClampRounded(double value, int min, int max)
        {
            int rounded = (int)Math.Round(value, MidpointRounding.AwayFromZero);
            return Math.Clamp(rounded, Math.Min(min, max), Math.Max(min, max));
        }

        private static List<string> NormalizeProperties(IEnumerable<string> properties)
        {
            string[] order = ["x", "y", "skewX", "skewY", "scaleX", "scaleY", "alpha"];
            HashSet<string> set = new(StringComparer.OrdinalIgnoreCase);
            if (properties is not null)
            {
                foreach (string property in properties)
                {
                    if (!string.IsNullOrWhiteSpace(property))
                    {
                        set.Add(property.Trim());
                    }
                }
            }

            return order.Where(set.Contains).ToList();
        }

        private static string GetBooleanPropertyName(string propertyName)
        {
            return propertyName switch
            {
                "x" => nameof(TweenX),
                "y" => nameof(TweenY),
                "skewX" => nameof(TweenSkewX),
                "skewY" => nameof(TweenSkewY),
                "scaleX" => nameof(TweenScaleX),
                "scaleY" => nameof(TweenScaleY),
                "alpha" => nameof(TweenAlpha),
                _ => nameof(TweenX)
            };
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
