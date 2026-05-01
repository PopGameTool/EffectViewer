using CommunityToolkit.Mvvm.Input;

namespace EffectViewer.ViewModels
{
    public sealed partial class ReanimFrameHeaderViewModel : ViewModelBase
    {
        private readonly System.Action<int> _select;
        private bool _isPlayhead;

        public int FrameIndex { get; }
        public string Label => (FrameIndex + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);

        public bool IsPlayhead
        {
            get => _isPlayhead;
            set => SetProperty(ref _isPlayhead, value);
        }

        public ReanimFrameHeaderViewModel(int frameIndex, System.Action<int> select)
        {
            FrameIndex = frameIndex;
            _select = select;
        }

        [RelayCommand]
        private void Select()
        {
            _select?.Invoke(FrameIndex);
        }
    }
}
