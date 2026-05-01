using EffectViewer.Rendering.Gl;

namespace EffectViewer.Android
{
    internal sealed class AndroidGlInterfaceFactory : ProcAddressEffectGlInterfaceFactory
    {
        public AndroidGlInterfaceFactory()
            : base(EffectGlApi.OpenGlEs, "OpenGL ES")
        {
        }
    }
}
