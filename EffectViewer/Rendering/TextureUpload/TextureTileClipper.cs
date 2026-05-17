using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace EffectViewer.Rendering.TextureUpload
{
    public static class TextureTileClipper
    {
        private const float Epsilon = 0.000001f;

        public static IReadOnlyList<TextureTileDrawBatch> CreateBatches(
            TextureTileLayout layout,
            IReadOnlyList<RenderVertex> vertices)
        {
            ArgumentNullException.ThrowIfNull(layout);
            ArgumentNullException.ThrowIfNull(vertices);

            List<TextureTileDrawBatch> batches = [];
            Dictionary<int, TextureTileDrawBatch> batchByTile = new(layout.Tiles.Count);
            for (int i = 0; i + 2 < vertices.Count; i += 3)
            {
                AppendTriangle(layout, vertices[i], vertices[i + 1], vertices[i + 2], batches, batchByTile);
            }

            return batches;
        }

        public static IReadOnlyList<TextureTileDrawBatch> CreateBatchesFromSpan(
            TextureTileLayout layout,
            ReadOnlySpan<RenderVertex> vertices)
        {
            ArgumentNullException.ThrowIfNull(layout);

            List<TextureTileDrawBatch> batches = [];
            Dictionary<int, TextureTileDrawBatch> batchByTile = new(layout.Tiles.Count);
            for (int i = 0; i + 2 < vertices.Length; i += 3)
            {
                AppendTriangle(layout, vertices[i], vertices[i + 1], vertices[i + 2], batches, batchByTile);
            }

            return batches;
        }

        private static void AppendTriangle(
            TextureTileLayout layout,
            RenderVertex a,
            RenderVertex b,
            RenderVertex c,
            List<TextureTileDrawBatch> batches,
            Dictionary<int, TextureTileDrawBatch> batchByTile)
        {
            float minU = MathF.Min(a.Uv.X, MathF.Min(b.Uv.X, c.Uv.X));
            float maxU = MathF.Max(a.Uv.X, MathF.Max(b.Uv.X, c.Uv.X));
            float minV = MathF.Min(a.Uv.Y, MathF.Min(b.Uv.Y, c.Uv.Y));
            float maxV = MathF.Max(a.Uv.Y, MathF.Max(b.Uv.Y, c.Uv.Y));

            minU = Math.Clamp(minU, 0f, 1f);
            maxU = Math.Clamp(maxU, 0f, 1f);
            minV = Math.Clamp(minV, 0f, 1f);
            maxV = Math.Clamp(maxV, 0f, 1f);
            if (maxU < minU || maxV < minV)
            {
                return;
            }

            InlineArray8<ClipVertex> polygonBufferMemory = new InlineArray8<ClipVertex>();
            InlineArray8<ClipVertex> scratchBufferMemory = new InlineArray8<ClipVertex>();
            Span<ClipVertex> polygonBuffer = polygonBufferMemory;
            Span<ClipVertex> scratchBuffer = scratchBufferMemory;
            foreach (TextureTile tile in layout.Tiles)
            {
                if (tile.SourceRight < minU - Epsilon ||
                    tile.SourceLeft > maxU + Epsilon ||
                    tile.SourceBottom < minV - Epsilon ||
                    tile.SourceTop > maxV + Epsilon)
                {
                    continue;
                }

                AppendClippedTriangle(
                    tile,
                    a,
                    b,
                    c,
                    batches,
                    batchByTile,
                    polygonBuffer,
                    scratchBuffer);
            }
        }

        private static void AppendClippedTriangle(
            TextureTile tile,
            RenderVertex a,
            RenderVertex b,
            RenderVertex c,
            List<TextureTileDrawBatch> batches,
            Dictionary<int, TextureTileDrawBatch> batchByTile,
            Span<ClipVertex> polygonBuffer,
            Span<ClipVertex> scratchBuffer)
        {
            Span<ClipVertex> polygon = polygonBuffer;
            Span<ClipVertex> scratch = scratchBuffer;
            polygon[0] = ToClipVertex(a);
            polygon[1] = ToClipVertex(b);
            polygon[2] = ToClipVertex(c);

            int polygonCount = 3;
            polygonCount = ClipLeft(polygon, polygonCount, scratch, tile.SourceLeft);
            if (polygonCount < 3)
            {
                return;
            }

            Swap(ref polygon, ref scratch);
            polygonCount = ClipRight(polygon, polygonCount, scratch, tile.SourceRight);
            if (polygonCount < 3)
            {
                return;
            }

            Swap(ref polygon, ref scratch);
            polygonCount = ClipTop(polygon, polygonCount, scratch, tile.SourceTop);
            if (polygonCount < 3)
            {
                return;
            }

            Swap(ref polygon, ref scratch);
            polygonCount = ClipBottom(polygon, polygonCount, scratch, tile.SourceBottom);
            if (polygonCount < 3)
            {
                return;
            }

            Swap(ref polygon, ref scratch);

            TextureTileDrawBatch batch = GetOrAddBatch(tile, batches, batchByTile);
            ClipVertex first = polygon[0];
            for (int i = 1; i < polygonCount - 1; i++)
            {
                AppendMappedVertex(batch.Vertices, first, tile);
                AppendMappedVertex(batch.Vertices, polygon[i], tile);
                AppendMappedVertex(batch.Vertices, polygon[i + 1], tile);
            }
        }

        private static TextureTileDrawBatch GetOrAddBatch(
            TextureTile tile,
            List<TextureTileDrawBatch> batches,
            Dictionary<int, TextureTileDrawBatch> batchByTile)
        {
            if (batchByTile.TryGetValue(tile.Index, out TextureTileDrawBatch batch))
            {
                return batch;
            }

            batch = new TextureTileDrawBatch(tile);
            batchByTile[tile.Index] = batch;
            batches.Add(batch);
            return batch;
        }

        private static void AppendMappedVertex(List<RenderVertex> output, ClipVertex vertex, TextureTile tile)
        {
            output.Add(new RenderVertex(
                vertex.Position,
                new Vector2(tile.ToLocalU(vertex.Uv.X), tile.ToLocalV(vertex.Uv.Y)),
                vertex.Color));
        }

        private static ClipVertex ToClipVertex(RenderVertex vertex)
        {
            return new ClipVertex(vertex.Position, vertex.Uv, vertex.Color);
        }

        private static void Swap(ref Span<ClipVertex> left, ref Span<ClipVertex> right)
        {
            Span<ClipVertex> temp = left;
            left = right;
            right = temp;
        }

        private static int ClipLeft(ReadOnlySpan<ClipVertex> polygon, int polygonCount, Span<ClipVertex> output, float left)
        {
            return ClipU(polygon, polygonCount, output, left, keepGreater: true);
        }

        private static int ClipRight(ReadOnlySpan<ClipVertex> polygon, int polygonCount, Span<ClipVertex> output, float right)
        {
            return ClipU(polygon, polygonCount, output, right, keepGreater: false);
        }

        private static int ClipTop(ReadOnlySpan<ClipVertex> polygon, int polygonCount, Span<ClipVertex> output, float top)
        {
            return ClipV(polygon, polygonCount, output, top, keepGreater: true);
        }

        private static int ClipBottom(ReadOnlySpan<ClipVertex> polygon, int polygonCount, Span<ClipVertex> output, float bottom)
        {
            return ClipV(polygon, polygonCount, output, bottom, keepGreater: false);
        }

        private static int ClipU(
            ReadOnlySpan<ClipVertex> polygon,
            int polygonCount,
            Span<ClipVertex> output,
            float u,
            bool keepGreater)
        {
            if (polygonCount == 0)
            {
                return 0;
            }

            int outputCount = 0;
            ClipVertex previous = polygon[polygonCount - 1];
            bool previousInside = IsInsideU(previous, u, keepGreater);
            for (int i = 0; i < polygonCount; i++)
            {
                ClipVertex current = polygon[i];
                bool currentInside = IsInsideU(current, u, keepGreater);
                if (currentInside)
                {
                    if (!previousInside)
                    {
                        output[outputCount++] = IntersectU(previous, current, u);
                    }

                    output[outputCount++] = current;
                }
                else if (previousInside)
                {
                    output[outputCount++] = IntersectU(previous, current, u);
                }

                previous = current;
                previousInside = currentInside;
            }

            return outputCount;
        }

        private static int ClipV(
            ReadOnlySpan<ClipVertex> polygon,
            int polygonCount,
            Span<ClipVertex> output,
            float v,
            bool keepGreater)
        {
            if (polygonCount == 0)
            {
                return 0;
            }

            int outputCount = 0;
            ClipVertex previous = polygon[polygonCount - 1];
            bool previousInside = IsInsideV(previous, v, keepGreater);
            for (int i = 0; i < polygonCount; i++)
            {
                ClipVertex current = polygon[i];
                bool currentInside = IsInsideV(current, v, keepGreater);
                if (currentInside)
                {
                    if (!previousInside)
                    {
                        output[outputCount++] = IntersectV(previous, current, v);
                    }

                    output[outputCount++] = current;
                }
                else if (previousInside)
                {
                    output[outputCount++] = IntersectV(previous, current, v);
                }

                previous = current;
                previousInside = currentInside;
            }

            return outputCount;
        }

        private static bool IsInsideU(ClipVertex vertex, float u, bool keepGreater)
        {
            return keepGreater
                ? vertex.Uv.X >= u - Epsilon
                : vertex.Uv.X <= u + Epsilon;
        }

        private static bool IsInsideV(ClipVertex vertex, float v, bool keepGreater)
        {
            return keepGreater
                ? vertex.Uv.Y >= v - Epsilon
                : vertex.Uv.Y <= v + Epsilon;
        }

        private static ClipVertex IntersectU(ClipVertex a, ClipVertex b, float u)
        {
            float span = b.Uv.X - a.Uv.X;
            float amount = Math.Abs(span) <= Epsilon ? 0f : (u - a.Uv.X) / span;
            return Lerp(a, b, amount);
        }

        private static ClipVertex IntersectV(ClipVertex a, ClipVertex b, float v)
        {
            float span = b.Uv.Y - a.Uv.Y;
            float amount = Math.Abs(span) <= Epsilon ? 0f : (v - a.Uv.Y) / span;
            return Lerp(a, b, amount);
        }

        private static ClipVertex Lerp(ClipVertex a, ClipVertex b, float amount)
        {
            amount = Math.Clamp(amount, 0f, 1f);
            return new ClipVertex(
                Vector2.Lerp(a.Position, b.Position, amount),
                Vector2.Lerp(a.Uv, b.Uv, amount),
                Vector4.Lerp(a.Color, b.Color, amount));
        }

        private readonly record struct ClipVertex(Vector2 Position, Vector2 Uv, Vector4 Color);
    }

    public sealed class TextureTileDrawBatch
    {
        public TextureTileDrawBatch(TextureTile tile)
        {
            Tile = tile;
        }

        public TextureTile Tile { get; }
        public List<RenderVertex> Vertices { get; } = [];
    }
}
