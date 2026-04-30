using System.Numerics;

namespace EffectViewer.Rendering
{
    public readonly record struct RenderVertex(
        Vector2 Position,
        Vector2 Uv,
        Vector4 Color);
}
