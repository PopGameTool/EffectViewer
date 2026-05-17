using System;
using System.Collections.Generic;

namespace EffectViewer.Rendering
{
    public sealed class RenderMeshCommand
    {
        public RenderTextureRef Texture { get; }
        public IReadOnlyList<RenderVertex> Vertices { get; }
        public int VertexOffset { get; }
        public int VertexCount { get; }
        public RenderBlendMode BlendMode { get; }

        public RenderMeshCommand(RenderTextureRef texture, IReadOnlyList<RenderVertex> vertices, RenderBlendMode blendMode)
            : this(texture, vertices, 0, vertices?.Count ?? 0, blendMode)
        {
        }

        public RenderMeshCommand(
            RenderTextureRef texture,
            IReadOnlyList<RenderVertex> vertices,
            int vertexOffset,
            int vertexCount,
            RenderBlendMode blendMode)
        {
            ArgumentNullException.ThrowIfNull(vertices);
            if (vertexOffset < 0 ||
                vertexCount < 0 ||
                vertexOffset > vertices.Count ||
                vertexCount > vertices.Count - vertexOffset)
            {
                throw new ArgumentOutOfRangeException(nameof(vertexOffset));
            }

            Texture = texture;
            Vertices = vertices;
            VertexOffset = vertexOffset;
            VertexCount = vertexCount;
            BlendMode = blendMode;
        }

        public RenderVertex GetVertex(int index)
        {
            if ((uint)index >= (uint)VertexCount)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return Vertices[VertexOffset + index];
        }
    }
}
