using System.Collections.Generic;
using System.Linq;
using EffectViewer.Runtime.Showcase;
using MoonSharp.Interpreter;

namespace EffectViewer.Runtime.Lua
{
    public sealed class LuaHost
    {
        private readonly EffectWorld _world;

        static LuaHost()
        {
            UserData.RegisterType<LuaEffectApi>();
            UserData.RegisterType<LuaSceneApi>();
            UserData.RegisterType<SceneObject>();
            UserData.RegisterType<ShowcaseReanimation>();
            UserData.RegisterType<ShowcaseParticle>();
            UserData.RegisterType<ShowcaseTrail>();
        }

        public LuaHost(EffectWorld world)
        {
            _world = world;
        }

        public LuaRunResult Run(string code)
        {
            List<string> logs = [];
            ShowcaseScene scene = _world.BeginShowcase();

            try
            {
                Script script = new(CoreModules.Preset_SoftSandbox);
                script.Options.DebugPrint = message => logs.Add(message);
                script.Globals["effect"] = new LuaEffectApi(_world, scene, logs);
                script.Globals["scene"] = new LuaSceneApi(_world, logs);

                script.DoString(code);

                DynValue update = script.Globals.Get("update");
                if (update.Type == DataType.Function)
                {
                    script.Call(update, 1.0 / 60.0);
                }

                return new LuaRunResult(true, logs, _world.Objects.ToList(), scene);
            }
            catch (ScriptRuntimeException ex)
            {
                logs.Add(ex.DecoratedMessage);
                scene.Dispose();
                return new LuaRunResult(false, logs, _world.Objects.ToList(), null);
            }
            catch (SyntaxErrorException ex)
            {
                logs.Add(ex.DecoratedMessage);
                scene.Dispose();
                return new LuaRunResult(false, logs, _world.Objects.ToList(), null);
            }
        }
    }
}
