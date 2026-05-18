using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using EffectViewer.EffectRuntime.Graphics;
using EffectViewer.EffectRuntime.Common;
using InlineArray3TriVertex = System.Runtime.CompilerServices.InlineArray3<EffectViewer.EffectRuntime.Common.TriVertex>;

namespace EffectViewer.Rendering
{
    public sealed class FrameCaptureGraphics : Graphics
    {
        public const string WhiteTextureId = "__builtin_white_pixel";
        private const float ClipEpsilon = 0.0001f;
        private const int MaxClipPolygonVertexCount = 8;
        private const int TriangleVertexCount = 3;
        private const int QuadTriangleCount = 2;
        private const int MaxRenderVerticesPerClippedTriangle = (MaxClipPolygonVertexCount - 2) * TriangleVertexCount;

        private readonly RenderCommandWriter _commands;

        public FrameCaptureGraphics()
            : this(new RenderFrame())
        {
        }

        public FrameCaptureGraphics(RenderFrame frame)
        {
            _commands = new RenderCommandWriter(frame);
        }

        public RenderFrame Frame => _commands.Frame;

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

            Span<RenderVertex> vertices = _commands.AllocateMeshVertices(
                theVertices.Length * MaxRenderVerticesPerClippedTriangle,
                out int vertexOffset);
            int vertexCount = 0;
            Vector4 globalColor = ToVector4(mColorizeImages ? mColor : EffectColor.White);
            foreach (InlineArray3TriVertex triangle in theVertices)
            {
                AppendClippedTriangle(
                    vertices,
                    ref vertexCount,
                    ToClipVertex(triangle[0], globalColor),
                    ToClipVertex(triangle[1], globalColor),
                    ToClipVertex(triangle[2], globalColor),
                    mClipRect);
            }

            _commands.SetMeshVertexCount(vertexOffset + vertexCount);
            if (vertexCount == 0)
            {
                return;
            }

            _commands.AddMesh(
                new RenderTextureRef(theTexture.mId),
                vertexOffset,
                vertexCount,
                ToRenderBlendMode(mDrawMode));
        }

        public override void DrawImageMatrix(
            Image theImage,
            in Matrix4x4 theTransform,
            in Rectangle theClipRect,
            in EffectColor theColor,
            DrawMode theDrawMode,
            in Rectangle theSrcRect)
        {
            if (theImage == null ||
                string.IsNullOrWhiteSpace(theImage.mId) ||
                theSrcRect.Width <= 0 ||
                theSrcRect.Height <= 0 ||
                theClipRect.Width <= 0 ||
                theClipRect.Height <= 0 ||
                theColor.mAlpha <= 0)
            {
                return;
            }

            float halfWidth = theSrcRect.Width * 0.5f;
            float halfHeight = theSrcRect.Height * 0.5f;
            float left = -halfWidth;
            float top = -halfHeight;
            float right = halfWidth;
            float bottom = halfHeight;

            Vector2 topLeft = Vector2.Transform(new Vector2(left, top), theTransform);
            Vector2 topRight = Vector2.Transform(new Vector2(right, top), theTransform);
            Vector2 bottomRight = Vector2.Transform(new Vector2(right, bottom), theTransform);
            Vector2 bottomLeft = Vector2.Transform(new Vector2(left, bottom), theTransform);

            float textureWidth = Math.Max(1, theImage.mWidth);
            float textureHeight = Math.Max(1, theImage.mHeight);
            float u0 = theSrcRect.Left / textureWidth;
            float v0 = theSrcRect.Top / textureHeight;
            float u1 = theSrcRect.Right / textureWidth;
            float v1 = theSrcRect.Bottom / textureHeight;
            Vector4 color = ToVector4(theColor);

            Span<RenderVertex> vertices = _commands.AllocateMeshVertices(
                QuadTriangleCount * MaxRenderVerticesPerClippedTriangle,
                out int vertexOffset);
            int vertexCount = 0;
            AppendClippedTriangle(
                vertices,
                ref vertexCount,
                new ClipVertex(topLeft, new Vector2(u0, v0), color),
                new ClipVertex(topRight, new Vector2(u1, v0), color),
                new ClipVertex(bottomRight, new Vector2(u1, v1), color),
                theClipRect);
            AppendClippedTriangle(
                vertices,
                ref vertexCount,
                new ClipVertex(topLeft, new Vector2(u0, v0), color),
                new ClipVertex(bottomRight, new Vector2(u1, v1), color),
                new ClipVertex(bottomLeft, new Vector2(u0, v1), color),
                theClipRect);

            _commands.SetMeshVertexCount(vertexOffset + vertexCount);
            if (vertexCount == 0)
            {
                return;
            }

            _commands.AddMesh(
                new RenderTextureRef(theImage.mId),
                vertexOffset,
                vertexCount,
                ToRenderBlendMode(theDrawMode));
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

            Span<RenderVertex> vertices = _commands.AllocateMeshVertices(6, out int vertexOffset);
            vertices[0] = new RenderVertex(new Vector2(left, top), new Vector2(0f, 0f), color);
            vertices[1] = new RenderVertex(new Vector2(right, top), new Vector2(1f, 0f), color);
            vertices[2] = new RenderVertex(new Vector2(right, bottom), new Vector2(1f, 1f), color);
            vertices[3] = new RenderVertex(new Vector2(left, top), new Vector2(0f, 0f), color);
            vertices[4] = new RenderVertex(new Vector2(right, bottom), new Vector2(1f, 1f), color);
            vertices[5] = new RenderVertex(new Vector2(left, bottom), new Vector2(0f, 1f), color);

            _commands.AddMesh(
                new RenderTextureRef(WhiteTextureId),
                vertexOffset,
                6,
                ToRenderBlendMode(mDrawMode));
        }

        private ClipVertex ToClipVertex(in TriVertex source, Vector4 globalColor)
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
            Span<RenderVertex> output,
            ref int outputCount,
            ClipVertex a,
            ClipVertex b,
            ClipVertex c,
            Rectangle clipRect)
        {
            if (IsInside(a, clipRect) && IsInside(b, clipRect) && IsInside(c, clipRect))
            {
                output[outputCount++] = ToRenderVertex(a);
                output[outputCount++] = ToRenderVertex(b);
                output[outputCount++] = ToRenderVertex(c);
                return;
            }

            InlineArray8<ClipVertex> polygonMemory = new();
            InlineArray8<ClipVertex> scratchMemory = new();
            Span<ClipVertex> polygon = polygonMemory;
            Span<ClipVertex> scratch = scratchMemory;
            polygon[0] = a;
            polygon[1] = b;
            polygon[2] = c;

            int polygonCount = 3;
            polygonCount = ClipLeft(polygon, polygonCount, scratch, clipRect.Left);
            if (polygonCount < 3)
            {
                return;
            }

            Swap(ref polygon, ref scratch);
            polygonCount = ClipRight(polygon, polygonCount, scratch, clipRect.Right);
            if (polygonCount < 3)
            {
                return;
            }

            Swap(ref polygon, ref scratch);
            polygonCount = ClipTop(polygon, polygonCount, scratch, clipRect.Top);
            if (polygonCount < 3)
            {
                return;
            }

            Swap(ref polygon, ref scratch);
            polygonCount = ClipBottom(polygon, polygonCount, scratch, clipRect.Bottom);
            if (polygonCount < 3)
            {
                return;
            }

            Swap(ref polygon, ref scratch);

            ClipVertex first = polygon[0];
            for (int i = 1; i < polygonCount - 1; i++)
            {
                output[outputCount++] = ToRenderVertex(first);
                output[outputCount++] = ToRenderVertex(polygon[i]);
                output[outputCount++] = ToRenderVertex(polygon[i + 1]);
            }
        }

        private static void Swap(ref Span<ClipVertex> left, ref Span<ClipVertex> right)
        {
            Span<ClipVertex> temp = left;
            left = right;
            right = temp;
        }

        private static int ClipLeft(ReadOnlySpan<ClipVertex> input, int inputCount, Span<ClipVertex> output, float left)
        {
            return ClipX(input, inputCount, output, left, keepGreater: true);
        }

        private static int ClipRight(ReadOnlySpan<ClipVertex> input, int inputCount, Span<ClipVertex> output, float right)
        {
            return ClipX(input, inputCount, output, right, keepGreater: false);
        }

        private static int ClipTop(ReadOnlySpan<ClipVertex> input, int inputCount, Span<ClipVertex> output, float top)
        {
            return ClipY(input, inputCount, output, top, keepGreater: true);
        }

        private static int ClipBottom(ReadOnlySpan<ClipVertex> input, int inputCount, Span<ClipVertex> output, float bottom)
        {
            return ClipY(input, inputCount, output, bottom, keepGreater: false);
        }

        private static int ClipX(
            ReadOnlySpan<ClipVertex> input,
            int inputCount,
            Span<ClipVertex> output,
            float x,
            bool keepGreater)
        {
            if (inputCount == 0)
            {
                return 0;
            }

            int outputCount = 0;
            ClipVertex previous = input[inputCount - 1];
            bool previousInside = IsInsideX(previous, x, keepGreater);
            for (int i = 0; i < inputCount; i++)
            {
                ClipVertex current = input[i];
                bool currentInside = IsInsideX(current, x, keepGreater);
                if (currentInside)
                {
                    if (!previousInside)
                    {
                        output[outputCount++] = IntersectX(previous, current, x);
                    }

                    output[outputCount++] = current;
                }
                else if (previousInside)
                {
                    output[outputCount++] = IntersectX(previous, current, x);
                }

                previous = current;
                previousInside = currentInside;
            }

            return outputCount;
        }

        private static int ClipY(
            ReadOnlySpan<ClipVertex> input,
            int inputCount,
            Span<ClipVertex> output,
            float y,
            bool keepGreater)
        {
            if (inputCount == 0)
            {
                return 0;
            }

            int outputCount = 0;
            ClipVertex previous = input[inputCount - 1];
            bool previousInside = IsInsideY(previous, y, keepGreater);
            for (int i = 0; i < inputCount; i++)
            {
                ClipVertex current = input[i];
                bool currentInside = IsInsideY(current, y, keepGreater);
                if (currentInside)
                {
                    if (!previousInside)
                    {
                        output[outputCount++] = IntersectY(previous, current, y);
                    }

                    output[outputCount++] = current;
                }
                else if (previousInside)
                {
                    output[outputCount++] = IntersectY(previous, current, y);
                }

                previous = current;
                previousInside = currentInside;
            }

            return outputCount;
        }

        private static bool IsInside(ClipVertex vertex, Rectangle clipRect)
        {
            return vertex.Position.X >= clipRect.Left &&
                   vertex.Position.X <= clipRect.Right &&
                   vertex.Position.Y >= clipRect.Top &&
                   vertex.Position.Y <= clipRect.Bottom;
        }

        private static bool IsInsideX(ClipVertex vertex, float x, bool keepGreater)
        {
            return keepGreater ? vertex.Position.X >= x : vertex.Position.X <= x;
        }

        private static bool IsInsideY(ClipVertex vertex, float y, bool keepGreater)
        {
            return keepGreater ? vertex.Position.Y >= y : vertex.Position.Y <= y;
        }

        private static ClipVertex IntersectX(ClipVertex start, ClipVertex end, float x)
        {
            float delta = end.Position.X - start.Position.X;
            float t = Math.Abs(delta) <= ClipEpsilon ? 0f : (x - start.Position.X) / delta;
            return Lerp(start, end, Math.Clamp(t, 0f, 1f));
        }

        private static ClipVertex IntersectY(ClipVertex start, ClipVertex end, float y)
        {
            float delta = end.Position.Y - start.Position.Y;
            float t = Math.Abs(delta) <= ClipEpsilon ? 0f : (y - start.Position.Y) / delta;
            return Lerp(start, end, Math.Clamp(t, 0f, 1f));
        }

        private static ClipVertex Lerp(ClipVertex start, ClipVertex end, float t)
        {
            return new ClipVertex(
                Vector2.Lerp(start.Position, end.Position, t),
                Vector2.Lerp(start.Uv, end.Uv, t),
                Vector4.Lerp(start.Color, end.Color, t));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static RenderVertex ToRenderVertex(ClipVertex vertex)
        {
            return new RenderVertex(vertex.Position, vertex.Uv, vertex.Color);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsZeroColor(EffectColor color)
        {
            return color.mRed == 0 &&
                   color.mGreen == 0 &&
                   color.mBlue == 0 &&
                   color.mAlpha == 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector4 ToVector4(EffectColor color)
        {
            const float inv255 = 1f / 255f;
            return new Vector4(
                color.mRed * inv255,
                color.mGreen * inv255,
                color.mBlue * inv255,
                color.mAlpha * inv255);
        }

        private static RenderBlendMode ToRenderBlendMode(DrawMode drawMode)
        {
            return drawMode == DrawMode.Additive ? RenderBlendMode.Additive : RenderBlendMode.Normal;
        }

        private readonly record struct ClipVertex(Vector2 Position, Vector2 Uv, Vector4 Color);
    }
}
