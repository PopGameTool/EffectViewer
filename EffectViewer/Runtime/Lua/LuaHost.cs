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
            UserData.RegisterType<LuaGraphicsApi>();
            UserData.RegisterType<SceneObject>();
            UserData.RegisterType<ShowcaseReanimation>();
            UserData.RegisterType<ShowcaseReanimationTrack>();
            UserData.RegisterType<ShowcaseParticle>();
            UserData.RegisterType<ShowcaseParticleEmitter>();
            UserData.RegisterType<ShowcaseParticleInstance>();
            UserData.RegisterType<ShowcaseTrail>();
            UserData.RegisterType<ShowcaseTrailPoint>();
            UserData.RegisterType<ShowcaseAttachment>();
        }

        public LuaHost(EffectWorld world)
        {
            _world = world;
        }

        public LuaRunResult Run(string code)
        {
            return Run(code, null);
        }

        public LuaRunResult Run(string code, System.Action<string> logAdded)
        {
            IList<string> logs = logAdded is null ? [] : new LuaLogList(logAdded);
            ShowcaseScene scene = _world.BeginShowcase();

            try
            {
                Script script = new(CoreModules.Preset_SoftSandbox);
                script.Options.DebugPrint = message => logs.Add(message);
                script.Globals["effect"] = new LuaEffectApi(_world, scene, logs);
                script.Globals["scene"] = new LuaSceneApi(_world, logs);

                script.DoString(code);

                DynValue update = script.Globals.Get("update");
                DynValue draw = script.Globals.Get("draw");
                ValidateOptionalFunction(update, "update");
                ValidateOptionalFunction(draw, "draw");

                LuaShowcaseScript callbacks = null;
                if (update.Type == DataType.Function || draw.Type == DataType.Function)
                {
                    callbacks = new LuaShowcaseScript(script, update, draw, logs);
                    scene.SetScriptCallbacks(callbacks);
                }

                return new LuaRunResult(true, logs.ToList(), _world.Objects.ToList(), scene);
            }
            catch (ScriptRuntimeException ex)
            {
                logs.Add(ex.DecoratedMessage);
                scene.Dispose();
                return new LuaRunResult(false, logs.ToList(), _world.Objects.ToList(), null);
            }
            catch (SyntaxErrorException ex)
            {
                logs.Add(ex.DecoratedMessage);
                scene.Dispose();
                return new LuaRunResult(false, logs.ToList(), _world.Objects.ToList(), null);
            }
            catch (System.Exception ex)
            {
                logs.Add(ex.Message);
                scene.Dispose();
                return new LuaRunResult(false, logs.ToList(), _world.Objects.ToList(), null);
            }
        }

        private static void ValidateOptionalFunction(DynValue value, string name)
        {
            if (value.Type is DataType.Nil or DataType.Void or DataType.Function)
            {
                return;
            }

            throw new ScriptRuntimeException($"'{name}' must be a function when it is defined.");
        }
    }
}
