namespace EffectViewer.ViewModels
{
    public sealed class ReanimTrackViewModel : ViewModelBase
    {
        private bool _isVisible = true;

        public int Index { get; }
        public string Name { get; }

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

        public event System.Action<ReanimTrackViewModel> VisibilityChanged;

        public ReanimTrackViewModel(int index, string name)
        {
            Index = index;
            Name = name;
        }
    }
}
