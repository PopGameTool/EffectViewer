using System;
using EffectViewer.Rendering;
using EffectViewer.TodLib.Common;
using EffectViewer.TodLib.Graphics;
using MoonSharp.Interpreter;

namespace EffectViewer.Runtime.Lua
{
    public sealed class LuaGraphicsApi
    {
        private readonly FrameCaptureGraphics _graphics;

        internal LuaGraphicsApi(FrameCaptureGraphics graphics)
        {
            _graphics = graphics ?? throw new ArgumentNullException(nameof(graphics));
        }

        internal FrameCaptureGraphics Graphics => _graphics;

        public double trans_x
        {
            get => _graphics.mTransX;
            set => _graphics.mTransX = (float)value;
        }

        public double trans_y
        {
            get => _graphics.mTransY;
            set => _graphics.mTransY = (float)value;
        }

        public string mode
        {
            get => _graphics.mDrawMode == DrawMode.Additive ? "additive" : "normal";
            set => _graphics.mDrawMode = ParseDrawMode(value);
        }

        public LuaGraphicsApi set_color(double red, double green, double blue)
        {
            return set_color(red, green, blue, 255);
        }

        public LuaGraphicsApi set_color(double red, double green, double blue, double alpha)
        {
            _graphics.mColor = new SexyColor(
                ClampColor(red),
                ClampColor(green),
                ClampColor(blue),
                ClampColor(alpha));
            return this;
        }

        public LuaGraphicsApi set_draw_mode(string mode)
        {
            _graphics.mDrawMode = ParseDrawMode(mode);
            return this;
        }

        public LuaGraphicsApi set_translation(double x, double y)
        {
            _graphics.mTransX = (float)x;
            _graphics.mTransY = (float)y;
            return this;
        }

        public LuaGraphicsApi translate(double x, double y)
        {
            _graphics.mTransX += (float)x;
            _graphics.mTransY += (float)y;
            return this;
        }

        public LuaGraphicsApi fill_rect(double x, double y, double width, double height)
        {
            _graphics.FillRect(
                (int)Math.Round(x),
                (int)Math.Round(y),
                (int)Math.Round(width),
                (int)Math.Round(height));
            return this;
        }

        public LuaGraphicsApi set_clip_rect(double x, double y, double width, double height)
        {
            _graphics.mClipRect = new Rectangle(
                (int)Math.Round(x),
                (int)Math.Round(y),
                (int)Math.Round(width),
                (int)Math.Round(height));
            return this;
        }

        public LuaGraphicsApi clear_clip_rect()
        {
            _graphics.mClipRect = new Rectangle(-16384, -16384, 16384 * 3, 16384 * 3);
            return this;
        }

        public LuaGraphicsApi reset()
        {
            _graphics.mTransX = 0;
            _graphics.mTransY = 0;
            _graphics.mColor = SexyColor.White;
            _graphics.mDrawMode = DrawMode.Normal;
            clear_clip_rect();
            return this;
        }

        private static int ClampColor(double value)
        {
            return Math.Clamp((int)Math.Round(value), 0, 255);
        }

        private static DrawMode ParseDrawMode(string value)
        {
            string normalized = (value ?? string.Empty).Trim().ToLowerInvariant();
            return normalized switch
            {
                "add" or "additive" => DrawMode.Additive,
                _ => DrawMode.Normal
            };
        }
    }
}
