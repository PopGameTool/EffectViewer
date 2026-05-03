namespace EffectViewer.Rendering
{
    public interface ICompletableRenderFrameProvider : IRenderFrameProvider
    {
        bool IsComplete { get; }
    }
}
