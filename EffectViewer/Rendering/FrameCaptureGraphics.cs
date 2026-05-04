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
        private const float ClipEpsilon = 0.0001f;

        private readonly RenderFrame _frame = new();

        public RenderFrame Frame => _frame;

        public override void DrawTrianglesTex(Image theTexture, ReadOnlySpan<InlineArray3TriVertex> theVertices)
        {
            if (theTexture == null ||
                string.IsNullOrWhiteSpace(theTexture.mId) ||
                theVertices.Length == 0 ||
                mClipRect.Width <= 0 ||
                mClipRect.Height <= 0)
            {
                return;
            }

            List<RenderVertex> vertices = new(theVertices.Length * 3);
            Vector4 globalColor = ToVector4(mColorizeImages ? mColor : SexyColor.White);
            foreach (InlineArray3TriVertex triangle in theVertices)
            {
                AppendClippedTriangle(
                    vertices,
                    ToClipVertex(triangle[0], globalColor),
                    ToClipVertex(triangle[1], globalColor),
                    ToClipVertex(triangle[2], globalColor),
                    mClipRect);
            }

            if (vertices.Count == 0)
            {
                return;
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
            if (width <= 0 || height <= 0 || mColor.mAlpha == 0)
            {
                return;
            }

            float left = x + mTransX;
            float top = y + mTransY;
            float right = left + width;
            float bottom = top + height;
            if (!ClipRect(ref left, ref top, ref right, ref bottom, mClipRect))
            {
                return;
            }

            Vector4 color = ToVector4(mColor);

            List<RenderVertex> vertices =
            [
                new(new Vector2(left, top), new Vector2(0f, 0f), color),
                new(new Vector2(right, top), new Vector2(1f, 0f), color),
                new(new Vector2(right, bottom), new Vector2(1f, 1f), color),
                new(new Vector2(left, top), new Vector2(0f, 0f), color),
                new(new Vector2(right, bottom), new Vector2(1f, 1f), color),
                new(new Vector2(left, bottom), new Vector2(0f, 1f), color)
            ];

            _frame.Meshes.Add(new RenderMeshCommand(
                new RenderTextureRef(WhiteTextureId),
                vertices,
                ToRenderBlendMode(mDrawMode)));
        }

        private ClipVertex ToClipVertex(TriVertex source, Vector4 globalColor)
        {
            Vector4 color = IsZeroColor(source.Color)
                ? globalColor
                : ToVector4(source.Color);
            return new ClipVertex(
                new Vector2(source.Position.X + mTransX, source.Position.Y + mTransY),
                source.TextureCoordinate,
                color);
        }

        private static bool ClipRect(ref float left, ref float top, ref float right, ref float bottom, Rectangle clipRect)
        {
            if (clipRect.Width <= 0 || clipRect.Height <= 0 || right <= left || bottom <= top)
            {
                return false;
            }

            left = MathF.Max(left, clipRect.Left);
            top = MathF.Max(top, clipRect.Top);
            right = MathF.Min(right, clipRect.Right);
            bottom = MathF.Min(bottom, clipRect.Bottom);
            return right > left && bottom > top;
        }

        private static void AppendClippedTriangle(
            List<RenderVertex> output,
            ClipVertex a,
            ClipVertex b,
            ClipVertex c,
            Rectangle clipRect)
        {
            List<ClipVertex> polygon = [a, b, c];
            polygon = ClipPolygon(polygon, clipRect, ClipEdge.Left);
            polygon = ClipPolygon(polygon, clipRect, ClipEdge.Right);
            polygon = ClipPolygon(polygon, clipRect, ClipEdge.Top);
            polygon = ClipPolygon(polygon, clipRect, ClipEdge.Bottom);
            if (polygon.Count < 3)
            {
                return;
            }

            ClipVertex first = polygon[0];
            for (int i = 1; i < polygon.Count - 1; i++)
            {
                output.Add(ToRenderVertex(first));
                output.Add(ToRenderVertex(polygon[i]));
                output.Add(ToRenderVertex(polygon[i + 1]));
            }
        }

        private static List<ClipVertex> ClipPolygon(IReadOnlyList<ClipVertex> input, Rectangle clipRect, ClipEdge edge)
        {
            if (input.Count == 0)
            {
                return [];
            }

            List<ClipVertex> output = new(input.Count + 1);
            ClipVertex previous = input[^1];
            bool previousInside = IsInside(previous, clipRect, edge);
            for (int i = 0; i < input.Count; i++)
            {
                ClipVertex current = input[i];
                bool currentInside = IsInside(current, clipRect, edge);
                if (currentInside)
                {
                    if (!previousInside)
                    {
                        output.Add(Intersect(previous, current, clipRect, edge));
                    }

                    output.Add(current);
                }
                else if (previousInside)
                {
                    output.Add(Intersect(previous, current, clipRect, edge));
                }

                previous = current;
                previousInside = currentInside;
            }

            return output;
        }

        private static bool IsInside(ClipVertex vertex, Rectangle clipRect, ClipEdge edge)
        {
            return edge switch
            {
                ClipEdge.Left => vertex.Position.X >= clipRect.Left,
                ClipEdge.Right => vertex.Position.X <= clipRect.Right,
                ClipEdge.Top => vertex.Position.Y >= clipRect.Top,
                ClipEdge.Bottom => vertex.Position.Y <= clipRect.Bottom,
                _ => true
            };
        }

        private static ClipVertex Intersect(ClipVertex start, ClipVertex end, Rectangle clipRect, ClipEdge edge)
        {
            float boundary = edge switch
            {
                ClipEdge.Left => clipRect.Left,
                ClipEdge.Right => clipRect.Right,
                ClipEdge.Top => clipRect.Top,
                ClipEdge.Bottom => clipRect.Bottom,
                _ => 0f
            };
            float startValue = edge is ClipEdge.Left or ClipEdge.Right ? start.Position.X : start.Position.Y;
            float endValue = edge is ClipEdge.Left or ClipEdge.Right ? end.Position.X : end.Position.Y;
            float delta = endValue - startValue;
            float t = Math.Abs(delta) <= ClipEpsilon ? 0f : (boundary - startValue) / delta;
            return Lerp(start, end, Math.Clamp(t, 0f, 1f));
        }

        private static ClipVertex Lerp(ClipVertex start, ClipVertex end, float t)
        {
            return new ClipVertex(
                Vector2.Lerp(start.Position, end.Position, t),
                Vector2.Lerp(start.Uv, end.Uv, t),
                Vector4.Lerp(start.Color, end.Color, t));
        }

        private static RenderVertex ToRenderVertex(ClipVertex vertex)
        {
            return new RenderVertex(vertex.Position, vertex.Uv, vertex.Color);
        }

        private static bool IsZeroColor(SexyColor color)
        {
            return color.mRed == 0 &&
                   color.mGreen == 0 &&
                   color.mBlue == 0 &&
                   color.mAlpha == 0;
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

        private readonly record struct ClipVertex(Vector2 Position, Vector2 Uv, Vector4 Color);

        private enum ClipEdge
        {
            Left,
            Right,
            Top,
            Bottom
        }
    }
}
