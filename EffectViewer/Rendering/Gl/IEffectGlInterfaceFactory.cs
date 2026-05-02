using Avalonia.OpenGL;

namespace EffectViewer.Rendering.Gl
{
    public interface IEffectGlInterfaceFactory
    {
        IEffectGlInterface Create(GlInterface platformGlInterface);
    }
}
