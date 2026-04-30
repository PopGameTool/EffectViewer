namespace EffectViewer.Rendering
{
    public interface ITextureCache
    {
        bool TryGetTexture(RenderTextureRef texture, out int handle);
        void Invalidate(RenderTextureRef texture);
        void Clear();
    }
}
