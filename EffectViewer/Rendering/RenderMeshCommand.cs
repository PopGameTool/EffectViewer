using System.Collections.Generic;

namespace EffectViewer.Rendering
{
    public sealed class RenderMeshCommand
    {
        public RenderTextureRef Texture { get; }
        public IReadOnlyList<RenderVertex> Vertices { get; }
        public RenderBlendMode BlendMode { get; }

        public RenderMeshCommand(RenderTextureRef texture, IReadOnlyList<RenderVertex> vertices, RenderBlendMode blendMode)
        {
            Texture = texture;
            Vertices = vertices;
            BlendMode = blendMode;
        }
    }
}
