using System;
using System.Collections.Generic;
using System.Numerics;

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

            int vertexOffset = MeshVertices.Count;
            MeshVertices.EnsureCapacity(vertexOffset + vertices.Count);
            foreach (RenderVertex vertex in vertices)
            {
                MeshVertices.Add(vertex);
            }

            AddMeshCommand(texture, vertexOffset, vertices.Count, blendMode);
        }

        public void AddMesh(RenderTextureRef texture, ReadOnlySpan<RenderVertex> vertices, RenderBlendMode blendMode)
        {
            if (vertices.Length == 0)
            {
                return;
            }

            int vertexOffset = MeshVertices.Count;
            MeshVertices.EnsureCapacity(vertexOffset + vertices.Length);
            foreach (RenderVertex vertex in vertices)
            {
                MeshVertices.Add(vertex);
            }

            AddMeshCommand(texture, vertexOffset, vertices.Length, blendMode);
        }

        public void AddMesh(RenderMeshCommand mesh)
        {
            ArgumentNullException.ThrowIfNull(mesh);
            if (mesh.VertexCount == 0)
            {
                return;
            }

            int vertexOffset = MeshVertices.Count;
            MeshVertices.EnsureCapacity(vertexOffset + mesh.VertexCount);
            for (int i = 0; i < mesh.VertexCount; i++)
            {
                MeshVertices.Add(mesh.GetVertex(i));
            }

            AddMeshCommand(mesh.Texture, vertexOffset, mesh.VertexCount, mesh.BlendMode);
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

            Meshes.Add(new RenderMeshCommand(texture, MeshVertices, vertexOffset, vertexCount, blendMode));
        }
    }
}
