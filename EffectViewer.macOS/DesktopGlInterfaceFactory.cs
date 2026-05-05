using EffectViewer.Rendering.Gl;

namespace EffectViewer.macOS
{
    internal sealed class DesktopGlInterfaceFactory : ProcAddressEffectGlInterfaceFactory
    {
        public DesktopGlInterfaceFactory()
            : base(EffectGlApi.OpenGl, "OpenGL")
        {
        }
    }
}
