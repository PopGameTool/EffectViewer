using System;
using TodFont = EffectViewer.TodLib.Graphics.Font;

namespace EffectViewer.Runtime.Showcase
{
    public sealed class ShowcaseFont
    {
        internal ShowcaseFont(TodFont font)
        {
            Font = font;
        }

        internal TodFont Font { get; }

        public string id => Font?.mId ?? string.Empty;
        public bool is_true_type => Font?.IsTrueType == true;
        public bool supports_chinese => Font?.SupportsChinese == true;
        public bool is_initialized => Font?.IsInitialized == true;
        public int true_type_font_size => Font?.TrueTypeFontSize ?? 0;
        public int true_type_border_size => Font?.TrueTypeBorderSize ?? 0;
        public int true_type_glyph_count => Font?.TrueTypeGlyphCount ?? 0;
        public double ascent => Font?.mAscent ?? 0;
        public double ascent_padding => Font?.mAscentPadding ?? 0;
        public double height => Font?.mHeight ?? 0;
        public double line_spacing_offset => Font?.mLineSpacingOffset ?? 0;
        public int default_point_size => Font?.mDefaultPointSize ?? 0;

        public int point_size
        {
            get => Font?.mPointSize ?? 0;
            set
            {
                if (Font is null)
                {
                    return;
                }

                Font.mPointSize = Math.Max(1, value);
                if (!Font.IsTrueType)
                {
                    Font.RecalculateMetrics();
                }
            }
        }

        public double scale
        {
            get => Font?.mScale ?? 1d;
            set
            {
                if (Font is null)
                {
                    return;
                }

                Font.mScale = (float)Math.Max(0.0001d, value);
                if (!Font.IsTrueType)
                {
                    Font.RecalculateMetrics();
                }
            }
        }

        public int string_width(string text)
        {
            return Font?.StringWidth(text ?? string.Empty) ?? 0;
        }

        public int char_width(string text)
        {
            return Font?.CharWidth(FirstChar(text)) ?? 0;
        }

        public int char_width_kern(string text, string previous)
        {
            return Font?.CharWidthKern(FirstChar(text), FirstChar(previous)) ?? 0;
        }

        private static char FirstChar(string text)
        {
            return string.IsNullOrEmpty(text) ? '\0' : text[0];
        }
    }
}
