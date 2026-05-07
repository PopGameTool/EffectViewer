using EffectViewer.Rendering.Gl;

namespace EffectViewer.Windows
{
    internal sealed class WindowsGlInterfaceFactory : ProcAddressEffectGlInterfaceFactory
    {
        public WindowsGlInterfaceFactory()
            : base(EffectGlApi.OpenGl, "OpenGL")
        {
        }
    }
}
