using System;
using System.Collections.Generic;
using System.Numerics;

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
            Dictionary<int, TextureTileDrawBatch> batchByTile = [];
            for (int i = 0; i + 2 < vertices.Count; i += 3)
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

            foreach (TextureTile tile in layout.Tiles)
            {
                if (tile.SourceRight < minU - Epsilon ||
                    tile.SourceLeft > maxU + Epsilon ||
                    tile.SourceBottom < minV - Epsilon ||
                    tile.SourceTop > maxV + Epsilon)
                {
                    continue;
                }

                List<ClipVertex> polygon =
                [
                    ToClipVertex(a),
                    ToClipVertex(b),
                    ToClipVertex(c)
                ];

                polygon = ClipLeft(polygon, tile.SourceLeft);
                polygon = ClipRight(polygon, tile.SourceRight);
                polygon = ClipTop(polygon, tile.SourceTop);
                polygon = ClipBottom(polygon, tile.SourceBottom);
                if (polygon.Count < 3)
                {
                    continue;
                }

                TextureTileDrawBatch batch = GetOrAddBatch(tile, batches, batchByTile);
                ClipVertex first = polygon[0];
                for (int i = 1; i < polygon.Count - 1; i++)
                {
                    AppendMappedVertex(batch.Vertices, first, tile);
                    AppendMappedVertex(batch.Vertices, polygon[i], tile);
                    AppendMappedVertex(batch.Vertices, polygon[i + 1], tile);
                }
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

        private static List<ClipVertex> ClipLeft(List<ClipVertex> polygon, float left)
        {
            return Clip(polygon, vertex => vertex.Uv.X >= left - Epsilon, (a, b) => IntersectU(a, b, left));
        }

        private static List<ClipVertex> ClipRight(List<ClipVertex> polygon, float right)
        {
            return Clip(polygon, vertex => vertex.Uv.X <= right + Epsilon, (a, b) => IntersectU(a, b, right));
        }

        private static List<ClipVertex> ClipTop(List<ClipVertex> polygon, float top)
        {
            return Clip(polygon, vertex => vertex.Uv.Y >= top - Epsilon, (a, b) => IntersectV(a, b, top));
        }

        private static List<ClipVertex> ClipBottom(List<ClipVertex> polygon, float bottom)
        {
            return Clip(polygon, vertex => vertex.Uv.Y <= bottom + Epsilon, (a, b) => IntersectV(a, b, bottom));
        }

        private static List<ClipVertex> Clip(
            List<ClipVertex> polygon,
            Func<ClipVertex, bool> inside,
            Func<ClipVertex, ClipVertex, ClipVertex> intersect)
        {
            if (polygon.Count == 0)
            {
                return [];
            }

            List<ClipVertex> output = [];
            ClipVertex previous = polygon[^1];
            bool previousInside = inside(previous);
            foreach (ClipVertex current in polygon)
            {
                bool currentInside = inside(current);
                if (currentInside)
                {
                    if (!previousInside)
                    {
                        output.Add(intersect(previous, current));
                    }

                    output.Add(current);
                }
                else if (previousInside)
                {
                    output.Add(intersect(previous, current));
                }

                previous = current;
                previousInside = currentInside;
            }

            return output;
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
