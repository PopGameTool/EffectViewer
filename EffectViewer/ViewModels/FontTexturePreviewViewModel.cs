using EffectViewer.Rendering;

namespace EffectViewer.ViewModels
{
    public sealed class FontTexturePreviewViewModel
    {
        public FontTexturePreviewViewModel(
            string layerName,
            string imageName,
            string imageId,
            string path,
            int width,
            int height)
        {
            LayerName = layerName ?? string.Empty;
            ImageName = imageName ?? string.Empty;
            ImageId = imageId ?? string.Empty;
            Path = path ?? string.Empty;
            Width = width;
            Height = height;
            PreviewFrame = EffectPreviewFrameBuilder.BuildFontTexturePreview(ImageId, Width, Height);
        }

        public string LayerName { get; }
        public string ImageName { get; }
        public string ImageId { get; }
        public string Path { get; }
        public int Width { get; }
        public int Height { get; }
        public RenderFrame PreviewFrame { get; }
        public bool IsResolved => !string.IsNullOrWhiteSpace(ImageId);
        public string DisplayName => string.IsNullOrWhiteSpace(LayerName)
            ? ImageId
            : $"{LayerName} / {ImageId}";
        public string TextureSize => Width > 0 && Height > 0 ? $"{Width} x {Height}" : "-";
    }
}
