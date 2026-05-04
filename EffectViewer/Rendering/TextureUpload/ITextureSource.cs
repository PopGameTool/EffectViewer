namespace EffectViewer.Rendering.TextureUpload
{
    public interface ITextureSource
    {
        bool TryLoad(RenderTextureRef texture, out TextureUploadData data);
    }

    public interface ITextureRevisionSource
    {
        int GetTextureRevision(RenderTextureRef texture);
    }
}
