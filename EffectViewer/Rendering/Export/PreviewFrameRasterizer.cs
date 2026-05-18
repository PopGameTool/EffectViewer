using System;
using System.Numerics;
using EffectViewer.Rendering.TextureUpload;

namespace EffectViewer.Rendering.Export
{
    public static class PreviewFrameRasterizer
    {
        private const float Epsilon = 0.00001f;

        public static byte[] Render(RenderFrame frame, ITextureSource textureSource, int width, int height)
        {
            return Render(frame, textureSource, width, height, PreviewExportTransform.Identity, transparentBackground: false);
        }

        public static byte[] Render(
            RenderFrame frame,
            ITextureSource textureSource,
            int width,
            int height,
            PreviewExportTransform transform,
            bool transparentBackground)
        {
            return Render(frame, textureSource, width, height, transform, transparentBackground, width, height);
        }

        public static byte[] Render(
            RenderFrame frame,
            ITextureSource textureSource,
            int width,
            int height,
            PreviewExportTransform transform,
            bool transparentBackground,
            int referenceWidth,
            int referenceHeight)
        {
            width = Math.Max(1, width);
            height = Math.Max(1, height);
            referenceWidth = Math.Max(1, referenceWidth);
            referenceHeight = Math.Max(1, referenceHeight);
            frame ??= new RenderFrame();
            textureSource ??= new GeneratedTextureSource();

            byte[] pixels = new byte[width * height * 4];
            if (!transparentBackground)
            {
                Clear(pixels, frame.ClearColor);
            }

            foreach (RenderSpriteCommand sprite in frame.Sprites)
            {
                DrawSprite(pixels, width, height, textureSource, sprite, transform, referenceWidth, referenceHeight);
            }

            foreach (RenderMeshCommand mesh in frame.Meshes)
            {
                DrawMesh(pixels, width, height, textureSource, mesh, frame.GetMeshVertices(mesh), transform);
            }

            return pixels;
        }

        private static void Clear(byte[] pixels, Vector4 color)
        {
            byte red = ToByte(color.X);
            byte green = ToByte(color.Y);
            byte blue = ToByte(color.Z);
            byte alpha = ToByte(color.W);

            for (int i = 0; i < pixels.Length; i += 4)
            {
                pixels[i + 0] = red;
                pixels[i + 1] = green;
                pixels[i + 2] = blue;
                pixels[i + 3] = alpha;
            }
        }

        private static void DrawSprite(
            byte[] target,
            int width,
            int height,
            ITextureSource textureSource,
            RenderSpriteCommand sprite,
            PreviewExportTransform transform,
            int referenceWidth,
            int referenceHeight)
        {
            if (!TryLoadTexture(textureSource, sprite.Texture, out TextureUploadData texture))
            {
                return;
            }

            float pixelX = sprite.Position.X <= 1f ? sprite.Position.X * referenceWidth : sprite.Position.X;
            float pixelY = sprite.Position.Y <= 1f ? sprite.Position.Y * referenceHeight : sprite.Position.Y;
            float pixelW = sprite.Size.X <= 1f ? sprite.Size.X * referenceWidth : sprite.Size.X;
            float pixelH = sprite.Size.Y <= 1f ? sprite.Size.Y * referenceHeight : sprite.Size.Y;
            pixelX = pixelX * transform.Scale + transform.OffsetX;
            pixelY = pixelY * transform.Scale + transform.OffsetY;
            pixelW *= transform.Scale;
            pixelH *= transform.Scale;
            if (Math.Abs(pixelW) < Epsilon || Math.Abs(pixelH) < Epsilon)
            {
                return;
            }

            float left = pixelX - pixelW / 2f;
            float right = pixelX + pixelW / 2f;
            float top = pixelY - pixelH / 2f;
            float bottom = pixelY + pixelH / 2f;
            DrawTexturedRect(target, width, height, texture, left, top, right, bottom, sprite.UvRect, sprite.Color, sprite.BlendMode);
        }

        private static void DrawTexturedRect(
            byte[] target,
            int width,
            int height,
            TextureUploadData texture,
            float left,
            float top,
            float right,
            float bottom,
            Vector4 uv,
            Vector4 color,
            RenderBlendMode blendMode)
        {
            if (right < left)
            {
                (left, right) = (right, left);
                (uv.X, uv.Z) = (uv.Z, uv.X);
            }

            if (bottom < top)
            {
                (top, bottom) = (bottom, top);
                (uv.Y, uv.W) = (uv.W, uv.Y);
            }

            int minX = Math.Clamp((int)Math.Floor(left), 0, width);
            int maxX = Math.Clamp((int)Math.Ceiling(right), 0, width);
            int minY = Math.Clamp((int)Math.Floor(top), 0, height);
            int maxY = Math.Clamp((int)Math.Ceiling(bottom), 0, height);
            float spanX = right - left;
            float spanY = bottom - top;
            if (spanX < Epsilon || spanY < Epsilon || minX >= maxX || minY >= maxY)
            {
                return;
            }

            for (int y = minY; y < maxY; y++)
            {
                float v = Lerp(uv.Y, uv.W, ((y + 0.5f) - top) / spanY);
                for (int x = minX; x < maxX; x++)
                {
                    float u = Lerp(uv.X, uv.Z, ((x + 0.5f) - left) / spanX);
                    Sample(texture, u, v, color, out float red, out float green, out float blue, out float alpha);
                    Blend(target, (y * width + x) * 4, red, green, blue, alpha, blendMode);
                }
            }
        }

        private static void DrawMesh(
            byte[] target,
            int width,
            int height,
            ITextureSource textureSource,
            RenderMeshCommand mesh,
            ReadOnlySpan<RenderVertex> vertices,
            PreviewExportTransform transform)
        {
            if (vertices.Length < 3 || !TryLoadTexture(textureSource, mesh.Texture, out TextureUploadData texture))
            {
                return;
            }

            for (int i = 0; i + 2 < vertices.Length; i += 3)
            {
                DrawTriangle(
                    target,
                    width,
                    height,
                    texture,
                    Transform(vertices[i], transform),
                    Transform(vertices[i + 1], transform),
                    Transform(vertices[i + 2], transform),
                    mesh.BlendMode);
            }
        }

        private static void DrawTriangle(
            byte[] target,
            int width,
            int height,
            TextureUploadData texture,
            RenderVertex a,
            RenderVertex b,
            RenderVertex c,
            RenderBlendMode blendMode)
        {
            float area = Edge(a.Position, b.Position, c.Position);
            if (Math.Abs(area) < Epsilon)
            {
                return;
            }

            int minX = Math.Clamp((int)Math.Floor(Math.Min(a.Position.X, Math.Min(b.Position.X, c.Position.X))), 0, width);
            int maxX = Math.Clamp((int)Math.Ceiling(Math.Max(a.Position.X, Math.Max(b.Position.X, c.Position.X))), 0, width);
            int minY = Math.Clamp((int)Math.Floor(Math.Min(a.Position.Y, Math.Min(b.Position.Y, c.Position.Y))), 0, height);
            int maxY = Math.Clamp((int)Math.Ceiling(Math.Max(a.Position.Y, Math.Max(b.Position.Y, c.Position.Y))), 0, height);
            if (minX >= maxX || minY >= maxY)
            {
                return;
            }

            for (int y = minY; y < maxY; y++)
            {
                for (int x = minX; x < maxX; x++)
                {
                    Vector2 point = new(x + 0.5f, y + 0.5f);
                    float wa = Edge(b.Position, c.Position, point) / area;
                    float wb = Edge(c.Position, a.Position, point) / area;
                    float wc = Edge(a.Position, b.Position, point) / area;
                    if (wa < -Epsilon || wb < -Epsilon || wc < -Epsilon)
                    {
                        continue;
                    }

                    Vector2 uv = a.Uv * wa + b.Uv * wb + c.Uv * wc;
                    Vector4 color = a.Color * wa + b.Color * wb + c.Color * wc;
                    Sample(texture, uv.X, uv.Y, color, out float red, out float green, out float blue, out float alpha);
                    Blend(target, (y * width + x) * 4, red, green, blue, alpha, blendMode);
                }
            }
        }

        private static bool TryLoadTexture(ITextureSource textureSource, RenderTextureRef texture, out TextureUploadData data)
        {
            return textureSource.TryLoad(texture, out data) &&
                   data is not null &&
                   data.Width > 0 &&
                   data.Height > 0 &&
                   data.RgbaPixels is not null &&
                   data.RgbaPixels.Length >= data.Width * data.Height * 4;
        }

        private static void Sample(
            TextureUploadData texture,
            float u,
            float v,
            Vector4 color,
            out float red,
            out float green,
            out float blue,
            out float alpha)
        {
            int x = Math.Clamp((int)MathF.Round(Math.Clamp(u, 0f, 1f) * (texture.Width - 1)), 0, texture.Width - 1);
            int y = Math.Clamp((int)MathF.Round(Math.Clamp(v, 0f, 1f) * (texture.Height - 1)), 0, texture.Height - 1);
            int offset = (y * texture.Width + x) * 4;
            red = Clamp01(texture.RgbaPixels[offset + 0] / 255f * color.X);
            green = Clamp01(texture.RgbaPixels[offset + 1] / 255f * color.Y);
            blue = Clamp01(texture.RgbaPixels[offset + 2] / 255f * color.Z);
            alpha = Clamp01(texture.RgbaPixels[offset + 3] / 255f * color.W);
        }

        private static void Blend(byte[] target, int offset, float red, float green, float blue, float alpha, RenderBlendMode blendMode)
        {
            float dstRed = target[offset + 0] / 255f;
            float dstGreen = target[offset + 1] / 255f;
            float dstBlue = target[offset + 2] / 255f;
            float dstAlpha = target[offset + 3] / 255f;

            float outRed;
            float outGreen;
            float outBlue;
            float outAlpha;
            if (blendMode == RenderBlendMode.Additive)
            {
                outRed = red * alpha + dstRed;
                outGreen = green * alpha + dstGreen;
                outBlue = blue * alpha + dstBlue;
                outAlpha = alpha + dstAlpha;
            }
            else
            {
                float inverse = 1f - alpha;
                outAlpha = alpha + dstAlpha * inverse;
                if (outAlpha <= Epsilon)
                {
                    outRed = 0f;
                    outGreen = 0f;
                    outBlue = 0f;
                }
                else
                {
                    outRed = (red * alpha + dstRed * dstAlpha * inverse) / outAlpha;
                    outGreen = (green * alpha + dstGreen * dstAlpha * inverse) / outAlpha;
                    outBlue = (blue * alpha + dstBlue * dstAlpha * inverse) / outAlpha;
                }
            }

            target[offset + 0] = ToByte(outRed);
            target[offset + 1] = ToByte(outGreen);
            target[offset + 2] = ToByte(outBlue);
            target[offset + 3] = ToByte(outAlpha);
        }

        private static float Edge(Vector2 a, Vector2 b, Vector2 c)
        {
            return (c.X - a.X) * (b.Y - a.Y) - (c.Y - a.Y) * (b.X - a.X);
        }

        private static RenderVertex Transform(RenderVertex vertex, PreviewExportTransform transform)
        {
            Vector2 position = new(
                vertex.Position.X * transform.Scale + transform.OffsetX,
                vertex.Position.Y * transform.Scale + transform.OffsetY);
            return new RenderVertex(position, vertex.Uv, vertex.Color);
        }

        private static float Lerp(float a, float b, float amount)
        {
            return a + (b - a) * amount;
        }

        private static float Clamp01(float value)
        {
            return Math.Clamp(value, 0f, 1f);
        }

        private static byte ToByte(float value)
        {
            return (byte)Math.Clamp((int)MathF.Round(Clamp01(value) * 255f), 0, 255);
        }
    }
}
