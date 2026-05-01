using CommunityToolkit.Mvvm.Input;

namespace EffectViewer.ViewModels
{
    public sealed partial class ReanimFrameCellViewModel : ViewModelBase
    {
        private readonly System.Action<int, int> _select;
        private bool _hasContent;
        private bool _hasImage;
        private bool _isSelected;
        private bool _isPlayhead;
        private string _tooltipText = string.Empty;

        public int TrackIndex { get; }
        public int FrameIndex { get; }
        public string Glyph => HasContent ? (HasImage ? "I" : "*") : string.Empty;

        public bool HasContent
        {
            get => _hasContent;
            private set
            {
                if (SetProperty(ref _hasContent, value))
                {
                    OnPropertyChanged(nameof(Glyph));
                }
            }
        }

        public bool HasImage
        {
            get => _hasImage;
            private set
            {
                if (SetProperty(ref _hasImage, value))
                {
                    OnPropertyChanged(nameof(Glyph));
                }
            }
        }

        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        public bool IsPlayhead
        {
            get => _isPlayhead;
            set => SetProperty(ref _isPlayhead, value);
        }

        public string TooltipText
        {
            get => _tooltipText;
            private set => SetProperty(ref _tooltipText, value ?? string.Empty);
        }

        public ReanimFrameCellViewModel(int trackIndex, int frameIndex, System.Action<int, int> select)
        {
            TrackIndex = trackIndex;
            FrameIndex = frameIndex;
            _select = select;
        }

        public void Update(bool hasContent, string imageId)
        {
            HasContent = hasContent;
            HasImage = hasContent && !string.IsNullOrWhiteSpace(imageId);
            TooltipText = string.IsNullOrWhiteSpace(imageId)
                ? $"Frame {FrameIndex + 1}: {(hasContent ? "Visible" : "Blank")}"
                : $"Frame {FrameIndex + 1}: {imageId}";
        }

        [RelayCommand]
        private void Select()
        {
            _select?.Invoke(TrackIndex, FrameIndex);
        }
    }
}
