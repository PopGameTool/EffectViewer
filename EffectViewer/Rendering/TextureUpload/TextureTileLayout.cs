using System;
using System.Collections.Generic;

namespace EffectViewer.Rendering.TextureUpload
{
    public sealed class TextureTileLayout
    {
        private const int TileGutterPixels = 1;

        private TextureTileLayout(int width, int height, IReadOnlyList<TextureTile> tiles)
        {
            Width = width;
            Height = height;
            Tiles = tiles;
        }

        public int Width { get; }
        public int Height { get; }
        public IReadOnlyList<TextureTile> Tiles { get; }
        public bool IsTiled => Tiles.Count > 1;

        public static TextureTileLayout Create(int width, int height, int maxTextureSize)
        {
            width = Math.Max(1, width);
            height = Math.Max(1, height);
            maxTextureSize = Math.Max(1, maxTextureSize);

            if (width <= maxTextureSize && height <= maxTextureSize)
            {
                TextureTile wholeTexture = new(
                    0,
                    0,
                    0,
                    width,
                    height,
                    0,
                    0,
                    width,
                    height,
                    width,
                    height);
                return new TextureTileLayout(width, height, [wholeTexture]);
            }

            int logicalTileSize = maxTextureSize <= TileGutterPixels * 2
                ? maxTextureSize
                : maxTextureSize - TileGutterPixels * 2;
            logicalTileSize = Math.Max(1, logicalTileSize);

            List<TextureTile> tiles = [];
            int index = 0;
            for (int y = 0; y < height; y += logicalTileSize)
            {
                int logicalHeight = Math.Min(logicalTileSize, height - y);
                int uploadY = Math.Max(0, y - TileGutterPixels);
                int uploadBottom = Math.Min(height, y + logicalHeight + TileGutterPixels);

                for (int x = 0; x < width; x += logicalTileSize)
                {
                    int logicalWidth = Math.Min(logicalTileSize, width - x);
                    int uploadX = Math.Max(0, x - TileGutterPixels);
                    int uploadRight = Math.Min(width, x + logicalWidth + TileGutterPixels);

                    tiles.Add(new TextureTile(
                        index++,
                        x,
                        y,
                        logicalWidth,
                        logicalHeight,
                        uploadX,
                        uploadY,
                        uploadRight - uploadX,
                        uploadBottom - uploadY,
                        width,
                        height));
                }
            }

            return new TextureTileLayout(width, height, tiles);
        }

        public static string GetTileTextureId(string textureId, TextureTile tile)
        {
            textureId ??= string.Empty;
            return tile.Index == 0 && tile.LogicalX == 0 && tile.LogicalY == 0 &&
                   tile.LogicalWidth == tile.OriginalWidth && tile.LogicalHeight == tile.OriginalHeight
                ? textureId
                : textureId + "#tile-" + tile.Index.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        public static byte[] CopyTilePixels(TextureUploadData data, TextureTile tile)
        {
            ArgumentNullException.ThrowIfNull(data);

            int byteCount = checked(tile.UploadWidth * tile.UploadHeight * 4);
            byte[] pixels = new byte[byteCount];
            int rowBytes = tile.UploadWidth * 4;
            for (int row = 0; row < tile.UploadHeight; row++)
            {
                int sourceOffset = ((tile.UploadY + row) * data.Width + tile.UploadX) * 4;
                int targetOffset = row * rowBytes;
                Buffer.BlockCopy(data.RgbaPixels, sourceOffset, pixels, targetOffset, rowBytes);
            }

            return pixels;
        }
    }

    public readonly record struct TextureTile(
        int Index,
        int LogicalX,
        int LogicalY,
        int LogicalWidth,
        int LogicalHeight,
        int UploadX,
        int UploadY,
        int UploadWidth,
        int UploadHeight,
        int OriginalWidth,
        int OriginalHeight)
    {
        public float SourceLeft => LogicalX / (float)OriginalWidth;
        public float SourceTop => LogicalY / (float)OriginalHeight;
        public float SourceRight => (LogicalX + LogicalWidth) / (float)OriginalWidth;
        public float SourceBottom => (LogicalY + LogicalHeight) / (float)OriginalHeight;

        public float ToLocalU(float u)
        {
            float sourceX = Math.Clamp(u, 0f, 1f) * OriginalWidth;
            return Math.Clamp((sourceX - UploadX) / UploadWidth, 0f, 1f);
        }

        public float ToLocalV(float v)
        {
            float sourceY = Math.Clamp(v, 0f, 1f) * OriginalHeight;
            return Math.Clamp((sourceY - UploadY) / UploadHeight, 0f, 1f);
        }
    }
}
