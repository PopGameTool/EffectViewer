using System;
using System.Numerics;
using EffectViewer.Rendering;
using EffectViewer.Runtime.Showcase;
using EffectViewer.TodLib.Common;
using EffectViewer.TodLib.Graphics;
using MoonSharp.Interpreter;
using InlineArray3TriVertex = System.Runtime.CompilerServices.InlineArray3<EffectViewer.TodLib.Common.TriVertex>;

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

        public string draw_mode
        {
            get => mode;
            set => mode = value;
        }

        public bool colorize_images
        {
            get => _graphics.mColorizeImages;
            set => _graphics.mColorizeImages = value;
        }

        public DynValue get_color()
        {
            return LuaApiUtility.ColorTuple(_graphics.mColor);
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

        public LuaGraphicsApi set_color(DynValue red, DynValue green, DynValue blue, DynValue alpha)
        {
            _graphics.mColor = LuaApiUtility.MergeColor(_graphics.mColor, red, green, blue, alpha);
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

        public DynValue get_clip_rect()
        {
            return LuaApiUtility.RectangleTuple(_graphics.mClipRect);
        }

        public LuaGraphicsApi clear_clip_rect()
        {
            _graphics.mClipRect = new Rectangle(-16384, -16384, 16384 * 3, 16384 * 3);
            return this;
        }

        public LuaGraphicsApi draw_image(ShowcaseImage image, double x, double y)
        {
            if (image?.Image is null)
            {
                return this;
            }

            int width = image.Image.GetCelWidth();
            int height = image.Image.GetCelHeight();
            Matrix4x4 transform = Matrix4x4.Identity;
            transform.M41 = (float)(x + width * 0.5d + _graphics.mTransX);
            transform.M42 = (float)(y + height * 0.5d + _graphics.mTransY);
            TodCommon.TodBltMatrix(
                _graphics,
                image.Image,
                transform,
                _graphics.mClipRect,
                GetImageColor(),
                _graphics.mDrawMode,
                new Rectangle(0, 0, width, height));
            return this;
        }

        public LuaGraphicsApi draw_image_matrix(
            ShowcaseImage image,
            ShowcaseMatrix transform,
            double src_rect_x,
            double src_rect_y,
            double src_rect_width,
            double src_rect_height)
        {
            if (image?.Image is null || transform is null)
            {
                return this;
            }

            Matrix4x4 matrix = transform.ToMatrix4x4();
            matrix.M41 += _graphics.mTransX;
            matrix.M42 += _graphics.mTransY;
            TodCommon.TodBltMatrix(
                _graphics,
                image.Image,
                matrix,
                _graphics.mClipRect,
                GetImageColor(),
                _graphics.mDrawMode,
                new Rectangle(
                    (int)Math.Round(src_rect_x),
                    (int)Math.Round(src_rect_y),
                    (int)Math.Round(src_rect_width),
                    (int)Math.Round(src_rect_height)));
            return this;
        }

        public LuaGraphicsApi draw_string(ShowcaseFont font, string msg, double x, double y)
        {
            if (font?.Font is null)
            {
                return this;
            }

            Matrix4x4 matrix = Matrix4x4.Identity;
            matrix.M41 = (float)(x + _graphics.mTransX);
            matrix.M42 = (float)(y + _graphics.mTransY);
            TodCommon.TodDrawStringMatrix(_graphics, font.Font, matrix, msg ?? string.Empty, _graphics.mColor);
            return this;
        }

        public LuaGraphicsApi draw_string_matrix(ShowcaseFont font, string msg, ShowcaseMatrix transform)
        {
            if (font?.Font is null || transform is null)
            {
                return this;
            }

            Matrix4x4 matrix = transform.ToMatrix4x4();
            matrix.M41 += _graphics.mTransX;
            matrix.M42 += _graphics.mTransY;
            TodCommon.TodDrawStringMatrix(_graphics, font.Font, matrix, msg ?? string.Empty, _graphics.mColor);
            return this;
        }

        public LuaGraphicsApi draw_triangles_tex(ShowcaseImage image, params ShowcaseTriVertex[] vertices)
        {
            if (image?.Image is null || vertices is null || vertices.Length < 3)
            {
                return this;
            }

            int triangleCount = vertices.Length / 3;
            InlineArray3TriVertex[] triangles = new InlineArray3TriVertex[triangleCount];
            for (int triangleIndex = 0; triangleIndex < triangleCount; triangleIndex++)
            {
                for (int vertexIndex = 0; vertexIndex < 3; vertexIndex++)
                {
                    TriVertex vertex = vertices[triangleIndex * 3 + vertexIndex].ToTriVertex();
                    triangles[triangleIndex][vertexIndex] = vertex;
                }
            }

            _graphics.DrawTrianglesTex(image.Image, triangles);
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

        private SexyColor GetImageColor()
        {
            return _graphics.mColorizeImages ? _graphics.mColor : SexyColor.White;
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
