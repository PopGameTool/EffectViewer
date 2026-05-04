using EffectViewer.Runtime.Showcase;

namespace EffectViewer.Runtime.Lua
{
    internal static class LuaApiRegistration
    {
        public static void RegisterAll()
        {
            LuaHost.RegisterLuaType<LuaSceneApi>();
            LuaHost.RegisterLuaType<LuaGraphicsApi>();
            LuaHost.RegisterLuaType<LuaAttachmentApi>();
            LuaHost.RegisterLuaType<SceneObject>();
            LuaHost.RegisterLuaType<ShowcaseAttachmentEffect>();
            LuaHost.RegisterLuaType<ShowcaseFrameTime>();
            LuaHost.RegisterLuaType<ShowcaseImage>();
            LuaHost.RegisterLuaType<ShowcaseMatrix>();
            LuaHost.RegisterLuaType<ShowcaseParticleRenderParams>();
            LuaHost.RegisterLuaType<ShowcaseReanimation>();
            LuaHost.RegisterLuaType<ShowcaseReanimationFrameRange>();
            LuaHost.RegisterLuaType<ShowcaseReanimationTrack>();
            LuaHost.RegisterLuaType<ShowcaseReanimationTransform>();
            LuaHost.RegisterLuaType<ShowcaseTriVertex>();
            LuaHost.RegisterLuaType<ShowcaseVector>();
            LuaHost.RegisterLuaType<ShowcaseVector3>();
            LuaHost.RegisterLuaType<ShowcaseParticle>();
            LuaHost.RegisterLuaType<ShowcaseParticleEmitter>();
            LuaHost.RegisterLuaType<ShowcaseParticleInstance>();
            LuaHost.RegisterLuaType<ShowcaseTrail>();
            LuaHost.RegisterLuaType<ShowcaseTrailPoint>();
            LuaHost.RegisterLuaType<ShowcaseAttachment>();
        }
    }
}
