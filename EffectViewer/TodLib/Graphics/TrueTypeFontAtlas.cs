using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using SkiaSharp;

namespace EffectViewer.TodLib.Graphics
{
    internal sealed class TrueTypeFontAtlas : IDisposable
    {
        private const int AtlasSize = 2048;
        private const int GlyphPadding = 2;
        private static readonly object sSharedLock = new();
        private static readonly Dictionary<string, TrueTypeFontAtlas> sSharedAtlases = new(StringComparer.Ordinal);
        private readonly string _textureId;
        private readonly string _sourcePath;
        private readonly SKTypeface _typeface;
        private readonly SKFont _font;
        private readonly SKPaint _fillPaint;
        private readonly SKPaint _strokePaint;
        private readonly SKBitmap _bitmap;
        private readonly SKCanvas _canvas;
        private readonly Dictionary<char, TrueTypeGlyphData> _glyphs = [];
        private int _nextX;
        private int _nextY;
        private int _rowHeight;
        private int _revision;
        private int _referenceCount;
        private bool _disposed;

        private TrueTypeFontAtlas(string id, string sourcePath, string textureId, SKTypeface typeface, int fontSize, int borderSize)
        {
            Id = id ?? string.Empty;
            _sourcePath = sourcePath ?? string.Empty;
            FontSize = fontSize;
            BorderSize = borderSize;
            _textureId = textureId;
            _typeface = typeface;
            _font = new SKFont(_typeface, FontSize)
            {
                Edging = SKFontEdging.Antialias,
                Hinting = SKFontHinting.Normal,
                Subpixel = true
            };
            _fillPaint = new SKPaint
            {
                Color = SKColors.White,
                IsAntialias = true,
                Style = SKPaintStyle.Fill
            };
            _strokePaint = new SKPaint
            {
                Color = SKColors.Black,
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = Math.Max(1, BorderSize * 2)
            };
            _bitmap = new SKBitmap(new SKImageInfo(AtlasSize, AtlasSize, SKColorType.Rgba8888, SKAlphaType.Unpremul));
            _canvas = new SKCanvas(_bitmap);
            ClearActiveAtlas();

            SKFontMetrics metrics = _font.Metrics;
            Ascent = MathF.Ceiling(MathF.Abs(metrics.Ascent)) + BorderSize + GlyphPadding;
            Height = MathF.Ceiling(metrics.Descent - metrics.Ascent) + BorderSize * 2 + GlyphPadding * 2;
            GlyphCount = _typeface.GlyphCount;
            SupportsChinese = ContainsGlyphs("中文汉字");
            PublishActiveAtlas();
        }

        public string Id { get; }
        public int FontSize { get; }
        public int BorderSize { get; }
        public float Ascent { get; }
        public float Height { get; }
        public int GlyphCount { get; }
        public bool SupportsChinese { get; }
        public bool IsInitialized => _typeface is not null && !_disposed;
        public string TextureId => _textureId;
        public Image TextureImage { get; private set; }

        public static TrueTypeFontAtlas Load(string id, string path, int fontSize, int borderSize)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return null;
            }

            string fullPath = Path.GetFullPath(path);
            string textureId = CreateTextureId(id);
            int resolvedFontSize = Math.Clamp(fontSize <= 0 ? 32 : fontSize, 1, 512);
            int resolvedBorderSize = Math.Clamp(borderSize, 0, 128);
            lock (sSharedLock)
            {
                if (sSharedAtlases.TryGetValue(textureId, out TrueTypeFontAtlas cached) &&
                    cached.Matches(fullPath, resolvedFontSize, resolvedBorderSize))
                {
                    cached._referenceCount++;
                    return cached;
                }

                SKTypeface typeface = SKTypeface.FromFile(fullPath);
                if (typeface is null)
                {
                    return null;
                }

                TrueTypeFontAtlas replacement = new(id, fullPath, textureId, typeface, resolvedFontSize, resolvedBorderSize)
                {
                    _referenceCount = 1
                };

                if (sSharedAtlases.TryGetValue(textureId, out cached))
                {
                    cached.DisposeInternal(removeTexture: false);
                }

                sSharedAtlases[textureId] = replacement;
                return replacement;
            }
        }

        public bool ContainsGlyphs(string text)
        {
            return !string.IsNullOrEmpty(text) && _font.ContainsGlyphs(text);
        }

        private bool Matches(string sourcePath, int fontSize, int borderSize)
        {
            return !_disposed &&
                FontSize == fontSize &&
                BorderSize == borderSize &&
                string.Equals(_sourcePath, sourcePath, StringComparison.OrdinalIgnoreCase);
        }

        public int StringWidth(string text)
        {
            if (string.IsNullOrEmpty(text) || !IsInitialized)
            {
                return 0;
            }

            int width = 0;
            foreach (char c in text)
            {
                if (!char.IsControl(c))
                {
                    width += CharWidth(c);
                }
            }

            return width;
        }

        public int CharWidth(char c)
        {
            return IsInitialized && !char.IsControl(c) && ContainsGlyph(c)
                ? MeasureLayoutAdvance(c.ToString())
                : 0;
        }

        public bool TryGetGlyph(char c, out TrueTypeGlyphData glyph)
        {
            return _glyphs.TryGetValue(c, out glyph);
        }

        public void PrepareText(string text)
        {
            if (string.IsNullOrEmpty(text) || !IsInitialized)
            {
                return;
            }

            List<char> missing = [];
            HashSet<char> missingSet = [];
            foreach (char c in text)
            {
                if (char.IsControl(c) || _glyphs.ContainsKey(c) || !missingSet.Add(c) || !ContainsGlyph(c))
                {
                    continue;
                }

                missing.Add(c);
            }

            if (missing.Count == 0)
            {
                return;
            }

            if (!TryCacheGlyphs(missing))
            {
                ClearActiveAtlas();
                TryCacheGlyphs(missing);
            }

            PublishActiveAtlas();
        }

        private bool TryCacheGlyphs(IReadOnlyList<char> glyphs)
        {
            foreach (char c in glyphs)
            {
                if (!TryCacheGlyph(c))
                {
                    return false;
                }
            }

            return true;
        }

        private bool TryCacheGlyph(char c)
        {
            string text = c.ToString();
            SKRect bounds = SKRect.Empty;
            float advance = _font.MeasureText(text, out bounds, _fillPaint);
            int leftPadding = BorderSize + GlyphPadding;
            int topPadding = BorderSize + GlyphPadding;
            int glyphWidth = Math.Max(1, (int)MathF.Ceiling(bounds.Width) + leftPadding * 2);
            int glyphHeight = Math.Max(1, (int)MathF.Ceiling(bounds.Height) + topPadding * 2);

            if (glyphWidth > AtlasSize || glyphHeight > AtlasSize)
            {
                return true;
            }

            if (_nextX + glyphWidth > AtlasSize)
            {
                _nextX = 0;
                _nextY += _rowHeight;
                _rowHeight = 0;
            }

            if (_nextY + glyphHeight > AtlasSize)
            {
                return false;
            }

            float drawX = _nextX + leftPadding - bounds.Left;
            float drawY = _nextY + topPadding - bounds.Top;
            if (BorderSize > 0)
            {
                _canvas.DrawText(text, drawX, drawY, SKTextAlign.Left, _font, _strokePaint);
            }

            _canvas.DrawText(text, drawX, drawY, SKTextAlign.Left, _font, _fillPaint);

            _glyphs[c] = new TrueTypeGlyphData(
                new Rectangle(_nextX, _nextY, glyphWidth, glyphHeight),
                (int)MathF.Floor(bounds.Left) - leftPadding,
                (int)MathF.Floor(bounds.Top) - topPadding,
                Math.Max(0, MeasureLayoutAdvance(advance)));

            _nextX += glyphWidth;
            _rowHeight = Math.Max(_rowHeight, glyphHeight);
            return true;
        }

        private bool ContainsGlyph(char c)
        {
            return _font.ContainsGlyph(c);
        }

        private int MeasureLayoutAdvance(string text)
        {
            return MeasureLayoutAdvance(_font.MeasureText(text, _fillPaint));
        }

        private int MeasureLayoutAdvance(float advance)
        {
            return (int)MathF.Ceiling(advance) + BorderSize * 2;
        }

        private void ClearActiveAtlas()
        {
            _glyphs.Clear();
            _canvas.Clear(SKColors.Transparent);
            _nextX = 0;
            _nextY = 0;
            _rowHeight = 0;
        }

        private void PublishActiveAtlas()
        {
            string textureId = TextureId;
            _revision = TrueTypeFontTextureRegistry.Set(textureId, AtlasSize, AtlasSize, CopyBitmapPixels());

            TextureImage = new Image
            {
                mId = textureId,
                mWidth = AtlasSize,
                mHeight = AtlasSize,
                mNumCols = 1,
                mNumRows = 1
            };
        }

        private byte[] CopyBitmapPixels()
        {
            byte[] pixels = new byte[_bitmap.ByteCount];
            Marshal.Copy(_bitmap.GetPixels(), pixels, 0, pixels.Length);
            return pixels;
        }

        private static string CreateTextureId(string id)
        {
            return "__ttf_font_" + SanitizeTextureId(id ?? string.Empty);
        }

        private static string SanitizeTextureId(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return "font";
            }

            char[] chars = id.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                if (!char.IsLetterOrDigit(chars[i]) && chars[i] != '_' && chars[i] != '-')
                {
                    chars[i] = '_';
                }
            }

            return new string(chars);
        }

        public void Dispose()
        {
            bool shouldDispose = false;
            bool removeTexture = false;
            lock (sSharedLock)
            {
                if (_referenceCount > 0)
                {
                    _referenceCount--;
                }

                if (_referenceCount == 0)
                {
                    shouldDispose = true;
                    if (sSharedAtlases.TryGetValue(TextureId, out TrueTypeFontAtlas cached) &&
                        ReferenceEquals(cached, this))
                    {
                        sSharedAtlases.Remove(TextureId);
                        removeTexture = true;
                    }
                }
            }

            if (shouldDispose)
            {
                DisposeInternal(removeTexture);
            }
        }

        private void DisposeInternal(bool removeTexture)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            if (removeTexture)
            {
                TrueTypeFontTextureRegistry.Remove(TextureId);
            }

            _canvas.Dispose();
            _bitmap.Dispose();
            _fillPaint.Dispose();
            _strokePaint.Dispose();
            _font.Dispose();
            _typeface.Dispose();
        }
    }

    internal readonly record struct TrueTypeGlyphData(
        Rectangle SourceRect,
        int Left,
        int Top,
        int Advance);
}
