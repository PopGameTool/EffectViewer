using System;
using System.Collections.Generic;
using System.Numerics;

namespace EffectViewer.Rendering.Export
{
    internal static class PreviewExportLayout
    {
        private const int MaxCanvasSize = 16384;

        public static PreviewExportCanvas CreateCanvas(IReadOnlyList<RenderFrame> frames, PreviewExportOptions options)
        {
            options = (options ?? new PreviewExportOptions()).Normalized();
            PreviewFrameBounds bounds = Measure(frames, options.ReferenceWidth, options.ReferenceHeight);
            if (!bounds.HasContent || bounds.Width <= 0f || bounds.Height <= 0f)
            {
                int fallbackWidth = Math.Clamp((int)Math.Ceiling(options.ReferenceWidth * options.CanvasScale), 1, MaxCanvasSize);
                int fallbackHeight = Math.Clamp((int)Math.Ceiling(options.ReferenceHeight * options.CanvasScale), 1, MaxCanvasSize);
                return new PreviewExportCanvas(fallbackWidth, fallbackHeight, PreviewExportTransform.Identity);
            }

            float requestedScale = (float)options.CanvasScale;
            float maxScaleX = MaxCanvasSize / bounds.Width;
            float maxScaleY = MaxCanvasSize / bounds.Height;
            float scale = Math.Max(0.0001f, Math.Min(requestedScale, Math.Min(maxScaleX, maxScaleY)));
            int width = Math.Clamp((int)Math.Ceiling(bounds.Width * scale), 1, MaxCanvasSize);
            int height = Math.Clamp((int)Math.Ceiling(bounds.Height * scale), 1, MaxCanvasSize);
            PreviewExportTransform transform = new(scale, -bounds.Left * scale, -bounds.Top * scale);
            return new PreviewExportCanvas(width, height, transform);
        }

        private static PreviewFrameBounds Measure(IReadOnlyList<RenderFrame> frames, int width, int height)
        {
            PreviewFrameBounds union = new();
            if (frames is null)
            {
                return union;
            }

            foreach (RenderFrame frame in frames)
            {
                union.Include(Measure(frame, width, height));
            }

            return union;
        }

        private static PreviewFrameBounds Measure(RenderFrame frame, int width, int height)
        {
            PreviewFrameBounds bounds = new();
            if (frame is null)
            {
                return bounds;
            }

            foreach (RenderSpriteCommand sprite in frame.Sprites)
            {
                float pixelX = sprite.Position.X <= 1f ? sprite.Position.X * width : sprite.Position.X;
                float pixelY = sprite.Position.Y <= 1f ? sprite.Position.Y * height : sprite.Position.Y;
                float pixelW = sprite.Size.X <= 1f ? sprite.Size.X * width : sprite.Size.X;
                float pixelH = sprite.Size.Y <= 1f ? sprite.Size.Y * height : sprite.Size.Y;
                bounds.Include(
                    pixelX - pixelW / 2f,
                    pixelY - pixelH / 2f,
                    pixelX + pixelW / 2f,
                    pixelY + pixelH / 2f);
            }

            foreach (RenderMeshCommand mesh in frame.Meshes)
            {
                for (int i = 0; i < mesh.VertexCount; i++)
                {
                    RenderVertex vertex = mesh.GetVertex(i);
                    Vector2 position = vertex.Position;
                    bounds.Include(position.X, position.Y);
                }
            }

            return bounds;
        }
    }
}
