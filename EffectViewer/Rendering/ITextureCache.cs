namespace EffectViewer.Rendering
{
    public interface ITextureCache
    {
        bool TryGetTexture(RenderTextureRef texture, int revision, out int handle);
        void Invalidate(RenderTextureRef texture);
        void Clear();
    }
}
