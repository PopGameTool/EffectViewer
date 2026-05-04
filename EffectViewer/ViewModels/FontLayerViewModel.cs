namespace EffectViewer.ViewModels
{
    public sealed class FontLayerViewModel
    {
        public FontLayerViewModel(
            string name,
            string imageName,
            string imageId,
            int pointSize,
            int ascent,
            int height,
            int spacing,
            int characterCount,
            int drawMode)
        {
            Name = name ?? string.Empty;
            ImageName = imageName ?? string.Empty;
            ImageId = imageId ?? string.Empty;
            PointSize = pointSize;
            Ascent = ascent;
            Height = height;
            Spacing = spacing;
            CharacterCount = characterCount;
            DrawMode = drawMode;
        }

        public string Name { get; }
        public string ImageName { get; }
        public string ImageId { get; }
        public int PointSize { get; }
        public int Ascent { get; }
        public int Height { get; }
        public int Spacing { get; }
        public int CharacterCount { get; }
        public int DrawMode { get; }
        public bool IsResolved => !string.IsNullOrWhiteSpace(ImageId);
        public string DisplayImageId => IsResolved ? ImageId : "-";
    }
}
