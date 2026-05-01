namespace EffectViewer.Rendering.Gl
{
    public interface IEffectGlInterfaceFactory
    {
        IEffectGlInterface Create(object platformGlInterface);
    }
}
