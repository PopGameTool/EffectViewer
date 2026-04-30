namespace EffectViewer.Rendering.WebGl
{
    public sealed class WebGlRendererPlan : IEffectRenderer
    {
        public string BackendName => "WebGL";

        public void Render(RenderFrame frame, int width, int height)
        {
            // Browser support should consume the same RenderFrame data through JS interop.
            // Keep this backend separate from OpenGlRenderer so WebGL limits stay explicit.
        }
    }
}
