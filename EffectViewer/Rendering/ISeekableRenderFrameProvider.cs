namespace EffectViewer.Rendering
{
    public interface ISeekableRenderFrameProvider : IRenderFrameProvider
    {
        RenderFrame GetFrameAtTime(double elapsedSeconds);
    }
}
