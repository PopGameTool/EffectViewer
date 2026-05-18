namespace EffectViewer.Rendering
{
    public readonly record struct RenderMeshCommand(
        RenderTextureRef Texture,
        int VertexOffset,
        int VertexCount,
        RenderBlendMode BlendMode);
}
