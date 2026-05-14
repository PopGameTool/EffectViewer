using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace EffectViewer.EffectRuntime.Graphics
{
    public class Font : IDisposable
    {
        private readonly Dictionary<char, char> _charMap = [];
        private TrueTypeFontAtlas _trueTypeAtlas;

        public string mId;
        public float mAscent;
        public float mAscentPadding;
        public float mHeight;
        public float mLineSpacingOffset;

        public int mDefaultPointSize;
        public int mPointSize;
        public float mScale = 1f;

        internal List<FontLayer> Layers { get; } = [];

        public bool IsTrueType => _trueTypeAtlas is not null;
        public bool SupportsChinese => _trueTypeAtlas?.SupportsChinese == true;
        public int TrueTypeFontSize => _trueTypeAtlas?.FontSize ?? 0;
        public int TrueTypeBorderSize => _trueTypeAtlas?.BorderSize ?? 0;
        public int TrueTypeGlyphCount => _trueTypeAtlas?.GlyphCount ?? 0;
        internal TrueTypeFontAtlas TrueTypeAtlas => _trueTypeAtlas;

        public bool IsInitialized => IsTrueType ? _trueTypeAtlas.IsInitialized : Layers.Count > 0;

        internal static Font LoadTrueType(string id, string path, int fontSize, int borderSize)
        {
            TrueTypeFontAtlas atlas = TrueTypeFontAtlas.Load(id, path, fontSize, borderSize);
            if (atlas is null)
            {
                return null;
            }

            Font font = new()
            {
                mId = id ?? string.Empty,
                mDefaultPointSize = atlas.FontSize,
                mPointSize = atlas.FontSize,
                _trueTypeAtlas = atlas
            };
            font.RecalculateMetrics();
            return font;
        }

        public int StringWidth(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return 0;
            }

            if (IsTrueType)
            {
                return _trueTypeAtlas.StringWidth(text);
            }

            int width = 0;
            char previous = '\0';
            foreach (char c in text)
            {
                width += CharWidthKern(c, previous);
                previous = c;
            }

            return width;
        }

        public int CharWidth(char c)
        {
            return CharWidthKern(c, '\0');
        }

        public int CharWidthKern(char c, char previous)
        {
            if (IsTrueType)
            {
                return _trueTypeAtlas.CharWidth(c);
            }

            char mapped = GetMappedChar(c);
            char mappedPrevious = previous == '\0' ? '\0' : GetMappedChar(previous);
            float pointSize = mPointSize * mScale;
            int maxWidth = 0;

            foreach (FontLayer layer in Layers)
            {
                FontCharData data = layer.GetCharData(mapped);
                int layerPointSize = layer.PointSize;
                int charWidth;
                int spacing;
                if (layerPointSize == 0)
                {
                    charWidth = (int)(data.Width * mScale);
                    spacing = mappedPrevious == '\0'
                        ? 0
                        : (int)((layer.Spacing + layer.GetCharData(mappedPrevious).GetKerning(mapped)) * mScale);
                }
                else
                {
                    charWidth = (int)(data.Width * pointSize / layerPointSize);
                    spacing = mappedPrevious == '\0'
                        ? 0
                        : (int)((layer.Spacing + layer.GetCharData(mappedPrevious).GetKerning(mapped)) * pointSize / layerPointSize);
                }

                maxWidth = Math.Max(maxWidth, charWidth + spacing);
            }

            return maxWidth;
        }

        internal void SetCharMap(Dictionary<char, char> charMap)
        {
            _charMap.Clear();
            foreach ((char key, char value) in charMap)
            {
                _charMap[key] = value;
            }
        }

        internal char GetMappedChar(char c)
        {
            return _charMap.TryGetValue(c, out char mapped) ? mapped : c;
        }

        internal void RecalculateMetrics()
        {
            if (IsTrueType)
            {
                mDefaultPointSize = _trueTypeAtlas.FontSize;
                mPointSize = _trueTypeAtlas.FontSize;
                mAscent = _trueTypeAtlas.Ascent;
                mAscentPadding = 0f;
                mHeight = _trueTypeAtlas.Height;
                mLineSpacingOffset = 0f;
                return;
            }

            mPointSize = mPointSize <= 0 ? mDefaultPointSize : mPointSize;
            if (mPointSize <= 0 && Layers.Count > 0)
            {
                mPointSize = Layers.Select(layer => layer.PointSize).FirstOrDefault(pointSize => pointSize > 0);
            }

            if (mPointSize <= 0)
            {
                mPointSize = 1;
            }

            mAscent = 0f;
            mAscentPadding = 0f;
            mHeight = 0f;
            mLineSpacingOffset = 0f;
            bool firstLayer = true;
            foreach (FontLayer layer in Layers)
            {
                float layerPointSize = layer.PointSize == 0 ? 1f : layer.PointSize;
                float pointSize = layer.PointSize == 0 ? mScale : mPointSize * mScale;
                float layerAscent = layer.Ascent * pointSize / layerPointSize;
                float layerHeight = (layer.Height != 0 ? layer.Height : layer.DefaultHeight) * pointSize / layerPointSize;
                float ascentPadding = layer.AscentPadding * pointSize / layerPointSize;
                float lineSpacingOffset = layer.LineSpacingOffset * pointSize / layerPointSize;

                mAscent = Math.Max(mAscent, layerAscent);
                mHeight = Math.Max(mHeight, layerHeight);
                mAscentPadding = firstLayer ? ascentPadding : Math.Min(mAscentPadding, ascentPadding);
                mLineSpacingOffset = firstLayer ? lineSpacingOffset : Math.Max(mLineSpacingOffset, lineSpacingOffset);
                firstLayer = false;
            }
        }

        internal void PrepareTrueTypeText(string text)
        {
            _trueTypeAtlas?.PrepareText(text);
        }

        internal bool TryGetTrueTypeGlyph(char c, out TrueTypeGlyphData glyph)
        {
            if (_trueTypeAtlas is not null)
            {
                return _trueTypeAtlas.TryGetGlyph(c, out glyph);
            }

            glyph = default;
            return false;
        }

        public void Dispose()
        {
            _trueTypeAtlas?.Dispose();
            _trueTypeAtlas = null;
        }
    }

    internal sealed class FontLayer
    {
        private readonly Dictionary<char, FontCharData> _charData = [];

        public string Name { get; init; } = string.Empty;
        public string ImageName { get; set; } = string.Empty;
        public Image Image { get; set; }
        public int DrawMode { get; set; } = -1;
        public Point Offset { get; set; }
        public int Spacing { get; set; }
        public int PointSize { get; set; }
        public int Ascent { get; set; }
        public int AscentPadding { get; set; }
        public int Height { get; set; }
        public int DefaultHeight { get; set; }
        public int LineSpacingOffset { get; set; }
        public int BaseOrder { get; set; }
        public EffectColor ColorMult { get; set; } = EffectColor.White;
        public EffectColor ColorAdd { get; set; } = new(0, 0, 0, 0);

        public IEnumerable<KeyValuePair<char, FontCharData>> CharData => _charData;

        public FontCharData GetCharData(char c)
        {
            if (!_charData.TryGetValue(c, out FontCharData data))
            {
                data = new FontCharData();
                _charData[c] = data;
            }

            return data;
        }
    }

    internal sealed class FontCharData
    {
        private readonly Dictionary<char, int> _kerningOffsets = [];

        public Rectangle ImageRect { get; set; }
        public Point Offset { get; set; }
        public int Width { get; set; }
        public int Order { get; set; }

        public int GetKerning(char next)
        {
            return _kerningOffsets.GetValueOrDefault(next);
        }

        public void SetKerning(char next, int offset)
        {
            _kerningOffsets[next] = offset;
        }
    }
}
