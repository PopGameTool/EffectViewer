using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;

namespace EffectViewer.Rendering
{
    public sealed class RenderFrame
    {
        public Vector4 ClearColor { get; set; } = new(0.08f, 0.09f, 0.1f, 1f);
        public List<RenderSpriteCommand> Sprites { get; } = [];
        public List<RenderMeshCommand> Meshes { get; } = [];
        internal List<RenderVertex> MeshVertices { get; } = [];

        public void AddMesh(RenderTextureRef texture, IReadOnlyList<RenderVertex> vertices, RenderBlendMode blendMode)
        {
            ArgumentNullException.ThrowIfNull(vertices);
            if (vertices.Count == 0)
            {
                return;
            }

            if (vertices is List<RenderVertex> vertexList)
            {
                AddMesh(texture, CollectionsMarshal.AsSpan(vertexList), blendMode);
                return;
            }

            if (vertices is RenderVertex[] vertexArray)
            {
                AddMesh(texture, vertexArray.AsSpan(), blendMode);
                return;
            }

            Span<RenderVertex> target = AllocateMeshVertices(vertices.Count, out int vertexOffset);
            for (int i = 0; i < vertices.Count; i++)
            {
                target[i] = vertices[i];
            }

            AddMeshCommand(texture, vertexOffset, vertices.Count, blendMode);
        }

        public void AddMesh(RenderTextureRef texture, ReadOnlySpan<RenderVertex> vertices, RenderBlendMode blendMode)
        {
            if (vertices.Length == 0)
            {
                return;
            }

            Span<RenderVertex> target = AllocateMeshVertices(vertices.Length, out int vertexOffset);
            vertices.CopyTo(target);

            AddMeshCommand(texture, vertexOffset, vertices.Length, blendMode);
        }

        public ReadOnlySpan<RenderVertex> GetMeshVertices(RenderMeshCommand mesh)
        {
            if (mesh.VertexOffset < 0 ||
                mesh.VertexCount < 0 ||
                mesh.VertexOffset > MeshVertices.Count ||
                mesh.VertexCount > MeshVertices.Count - mesh.VertexOffset)
            {
                throw new ArgumentOutOfRangeException(nameof(mesh));
            }

            return CollectionsMarshal.AsSpan(MeshVertices).Slice(mesh.VertexOffset, mesh.VertexCount);
        }

        public void ClearCommands()
        {
            Sprites.Clear();
            Meshes.Clear();
            MeshVertices.Clear();
        }

        internal void AddMeshCommand(
            RenderTextureRef texture,
            int vertexOffset,
            int vertexCount,
            RenderBlendMode blendMode)
        {
            if (vertexCount == 0)
            {
                return;
            }

            if (vertexOffset < 0 ||
                vertexCount < 0 ||
                vertexOffset > MeshVertices.Count ||
                vertexCount > MeshVertices.Count - vertexOffset)
            {
                throw new ArgumentOutOfRangeException(nameof(vertexOffset));
            }

            Meshes.Add(new RenderMeshCommand(texture, vertexOffset, vertexCount, blendMode));
        }

        internal Span<RenderVertex> AllocateMeshVertices(int vertexCount, out int vertexOffset)
        {
            if (vertexCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(vertexCount));
            }

            vertexOffset = MeshVertices.Count;
            if (vertexCount == 0)
            {
                return [];
            }

            MeshVertices.EnsureCapacity(vertexOffset + vertexCount);
            CollectionsMarshal.SetCount(MeshVertices, vertexOffset + vertexCount);
            return CollectionsMarshal.AsSpan(MeshVertices).Slice(vertexOffset, vertexCount);
        }

        internal void SetMeshVertexCount(int vertexCount)
        {
            if (vertexCount < 0 || vertexCount > MeshVertices.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(vertexCount));
            }

            CollectionsMarshal.SetCount(MeshVertices, vertexCount);
        }
    }
}
