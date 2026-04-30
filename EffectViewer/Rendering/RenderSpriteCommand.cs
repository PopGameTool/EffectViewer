using System.Numerics;

namespace EffectViewer.Rendering
{
    public readonly record struct RenderSpriteCommand(
        RenderTextureRef Texture,
        Vector2 Position,
        Vector2 Size,
        Vector4 UvRect,
        Vector4 Color,
        RenderBlendMode BlendMode);
}
