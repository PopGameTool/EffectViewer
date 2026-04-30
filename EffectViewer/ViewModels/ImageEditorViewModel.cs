using CommunityToolkit.Mvvm.ComponentModel;
using EffectViewer.Assets;
using EffectViewer.Projects;
using EffectViewer.Rendering;
using EffectViewer.Rendering.TextureUpload;
using Math = System.Math;

namespace EffectViewer.ViewModels
{
    public sealed partial class ImageEditorViewModel : EditorViewModelBase
    {
        public ImageAsset Asset { get; }

        [ObservableProperty]
        private int _rows;

        [ObservableProperty]
        private int _cols;

        [ObservableProperty]
        private int _frameIndex;

        public string AssetId => Asset.Id;
        public string Path => Asset.Path;
        public int CellCount => Rows * Cols;
        public int MaxFrameIndex => CellCount - 1;
        public int CurrentRow => Cols <= 0 ? 0 : FrameIndex / Cols;
        public int CurrentCol => Cols <= 0 ? 0 : FrameIndex % Cols;

        public ImageEditorViewModel(ImageAsset asset, EffectProject project)
            : base(asset.Id, EffectAssetKind.Image)
        {
            Asset = asset;
            _rows = asset.Rows;
            _cols = asset.Cols;
            _frameIndex = 0;
            RefreshPreviewFrame();
            TextureSource = new ProjectTextureSource(project);
        }

        partial void OnRowsChanged(int value)
        {
            Rows = value < 1 ? 1 : value;
            Asset.Rows = Rows;
            ClampFrameIndex();
            RefreshPreviewFrame();
            NotifyCellProperties();
        }

        partial void OnColsChanged(int value)
        {
            Cols = value < 1 ? 1 : value;
            Asset.Cols = Cols;
            ClampFrameIndex();
            RefreshPreviewFrame();
            NotifyCellProperties();
        }

        partial void OnFrameIndexChanged(int value)
        {
            if (value < 0 || value > MaxFrameIndex)
            {
                FrameIndex = Math.Clamp(value, 0, MaxFrameIndex);
                return;
            }

            RefreshPreviewFrame();
            OnPropertyChanged(nameof(CurrentRow));
            OnPropertyChanged(nameof(CurrentCol));
        }

        private void ClampFrameIndex()
        {
            if (FrameIndex < 0)
            {
                FrameIndex = 0;
            }
            else if (FrameIndex > MaxFrameIndex)
            {
                FrameIndex = MaxFrameIndex;
            }
        }

        private void RefreshPreviewFrame()
        {
            PreviewFrame = EffectPreviewFrameBuilder.BuildImagePreview(Asset.Id, Rows, Cols, FrameIndex);
            OnPropertyChanged(nameof(PreviewFrame));
        }

        private void NotifyCellProperties()
        {
            OnPropertyChanged(nameof(CellCount));
            OnPropertyChanged(nameof(MaxFrameIndex));
            OnPropertyChanged(nameof(CurrentRow));
            OnPropertyChanged(nameof(CurrentCol));
        }
    }
}
