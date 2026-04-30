namespace EffectViewer.Rendering
{
    public interface IRenderFrameProvider
    {
        RenderFrame GetFrame(double deltaSeconds);
    }
}
