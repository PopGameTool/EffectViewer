using System.Numerics;
using EffectViewer.Projects;
using Math = System.Math;

namespace EffectViewer.Rendering
{
    public static class EffectPreviewFrameBuilder
    {
        public static RenderFrame BuildPlaceholder(EffectAssetKind kind, string assetId)
        {
            RenderFrame frame = new();
            Vector4 color = kind switch
            {
                EffectAssetKind.Image => new Vector4(0.25f, 0.55f, 0.95f, 1f),
                EffectAssetKind.Reanim => new Vector4(0.85f, 0.55f, 0.2f, 1f),
                EffectAssetKind.Particle => new Vector4(0.95f, 0.35f, 0.18f, 1f),
                EffectAssetKind.Trail => new Vector4(0.3f, 0.82f, 0.62f, 1f),
                EffectAssetKind.Showcase => new Vector4(0.62f, 0.45f, 0.9f, 1f),
                _ => new Vector4(0.7f, 0.7f, 0.7f, 1f)
            };

            frame.Sprites.Add(new RenderSpriteCommand(
                new RenderTextureRef(assetId),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.35f, 0.35f),
                new Vector4(0f, 0f, 1f, 1f),
                color,
                RenderBlendMode.Normal));

            return frame;
        }

        public static RenderFrame BuildImagePreview(string assetId, int rows, int cols, int frameIndex = 0, int imageWidth = 0, int imageHeight = 0)
        {
            rows = Math.Max(1, rows);
            cols = Math.Max(1, cols);
            frameIndex = Math.Clamp(frameIndex, 0, rows * cols - 1);

            int row = frameIndex / cols;
            int col = frameIndex % cols;
            float left = col / (float)cols;
            float top = row / (float)rows;
            float right = (col + 1) / (float)cols;
            float bottom = (row + 1) / (float)rows;

            RenderFrame frame = new();
            Vector2 size = imageWidth > 0 && imageHeight > 0
                ? new Vector2(Math.Max(1, imageWidth / (float)cols), Math.Max(1, imageHeight / (float)rows))
                : new Vector2(256f, 256f);
            frame.Sprites.Add(new RenderSpriteCommand(
                new RenderTextureRef(assetId),
                new Vector2(0.5f, 0.5f),
                size,
                new Vector4(left, top, right, bottom),
                Vector4.One,
                RenderBlendMode.Normal));

            return frame;
        }
    }
}
