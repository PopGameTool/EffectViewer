using EffectViewer.Rendering.Gl;

namespace EffectViewer.iOS
{
    internal sealed class IosGlInterfaceFactory : ProcAddressEffectGlInterfaceFactory
    {
        public IosGlInterfaceFactory()
            : base(EffectGlApi.OpenGlEs, "OpenGL ES")
        {
        }
    }
}
