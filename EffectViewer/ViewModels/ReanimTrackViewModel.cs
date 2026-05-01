namespace EffectViewer.ViewModels
{
    public sealed class ReanimTrackViewModel : ViewModelBase
    {
        private string _name;
        private bool _isVisible = true;
        private bool _isSelected;

        public int Index { get; }

        public string Name
        {
            get => _name;
            set
            {
                string normalized = value ?? string.Empty;
                if (SetProperty(ref _name, normalized))
                {
                    OnPropertyChanged(nameof(DisplayName));
                    NameChanged?.Invoke(this);
                }
            }
        }

        public string DisplayName => string.IsNullOrWhiteSpace(Name)
            ? $"Track {Index + 1}"
            : $"{Index + 1}. {Name}";

        public bool IsVisible
        {
            get => _isVisible;
            set
            {
                if (SetProperty(ref _isVisible, value))
                {
                    VisibilityChanged?.Invoke(this);
                }
            }
        }

        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        public event System.Action<ReanimTrackViewModel> VisibilityChanged;
        public event System.Action<ReanimTrackViewModel> NameChanged;

        public ReanimTrackViewModel(int index, string name)
        {
            Index = index;
            _name = name ?? string.Empty;
        }
    }
}
