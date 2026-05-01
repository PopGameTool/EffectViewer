using EffectViewer.Rendering.Gl;

namespace EffectViewer.Browser
{
    internal sealed class WebGlInterfaceFactory : ProcAddressEffectGlInterfaceFactory
    {
        public WebGlInterfaceFactory()
            : base(EffectGlApi.WebGl, "WebGL")
        {
        }
    }
}
