using EffectViewer.Rendering.Gl;

namespace EffectViewer.Desktop
{
    internal sealed class DesktopGlInterfaceFactory : ProcAddressEffectGlInterfaceFactory
    {
        public DesktopGlInterfaceFactory()
            : base(EffectGlApi.OpenGl, "OpenGL")
        {
        }
    }
}
