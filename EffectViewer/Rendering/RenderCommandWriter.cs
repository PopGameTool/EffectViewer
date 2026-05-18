using System;

namespace EffectViewer.Rendering
{
    internal sealed class RenderCommandWriter
    {
        private readonly RenderFrame _frame;

        public RenderCommandWriter(RenderFrame frame)
        {
            _frame = frame ?? throw new ArgumentNullException(nameof(frame));
        }

        public RenderFrame Frame => _frame;

        public Span<RenderVertex> AllocateMeshVertices(int vertexCount, out int vertexOffset)
        {
            return _frame.AllocateMeshVertices(vertexCount, out vertexOffset);
        }

        public void SetMeshVertexCount(int vertexCount)
        {
            _frame.SetMeshVertexCount(vertexCount);
        }

        public void AddMesh(RenderTextureRef texture, int vertexOffset, int vertexCount, RenderBlendMode blendMode)
        {
            _frame.AddMeshCommand(texture, vertexOffset, vertexCount, blendMode);
        }
    }
}
