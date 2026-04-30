namespace EffectViewer.Rendering
{
    public interface IEffectRenderer
    {
        string BackendName { get; }
        void Render(RenderFrame frame, int width, int height);
    }
}
