using System;
using System.Collections.Generic;
using System.Numerics;
using EffectViewer.TodLib.Graphics;
using EffectViewer.TodLib.Common;
using InlineArray3TriVertex = System.Runtime.CompilerServices.InlineArray3<EffectViewer.TodLib.Common.TriVertex>;

namespace EffectViewer.Rendering
{
    public sealed class FrameCaptureGraphics : Graphics
    {
        public const string WhiteTextureId = "__builtin_white_pixel";

        private readonly RenderFrame _frame = new();

        public RenderFrame Frame => _frame;

        public override void DrawTrianglesTex(Image theTexture, ReadOnlySpan<InlineArray3TriVertex> theVertices)
        {
            if (theTexture == null || string.IsNullOrWhiteSpace(theTexture.mId) || theVertices.Length == 0)
            {
                return;
            }

            List<RenderVertex> vertices = new(theVertices.Length * 3);
            foreach (InlineArray3TriVertex triangle in theVertices)
            {
                for (int i = 0; i < 3; i++)
                {
                    TriVertex source = triangle[i];
                    vertices.Add(new RenderVertex(
                        new Vector2(source.Position.X, source.Position.Y),
                        source.TextureCoordinate,
                        ToVector4(source.Color)));
                }
            }

            _frame.Meshes.Add(new RenderMeshCommand(
                new RenderTextureRef(theTexture.mId),
                vertices,
                ToRenderBlendMode(mDrawMode)));
        }

        public override void FillRect(Rectangle rect)
        {
            FillRect(rect.X, rect.Y, rect.Width, rect.Height);
        }

        public override void FillRect(int x, int y, int width, int height)
        {
            if (width <= 0 || height <= 0)
            {
                return;
            }

            _frame.Sprites.Add(new RenderSpriteCommand(
                new RenderTextureRef(WhiteTextureId),
                new Vector2(x + mTransX + width * 0.5f, y + mTransY + height * 0.5f),
                new Vector2(width, height),
                new Vector4(0f, 0f, 1f, 1f),
                ToVector4(mColor),
                ToRenderBlendMode(mDrawMode)));
        }

        private static Vector4 ToVector4(SexyColor color)
        {
            return new Vector4(
                color.mRed / 255f,
                color.mGreen / 255f,
                color.mBlue / 255f,
                color.mAlpha / 255f);
        }

        private static RenderBlendMode ToRenderBlendMode(DrawMode drawMode)
        {
            return drawMode == DrawMode.Additive ? RenderBlendMode.Additive : RenderBlendMode.Normal;
        }
    }
}
