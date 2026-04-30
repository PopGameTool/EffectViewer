using System.Collections.Generic;
using System.Linq;
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
        }

        public LuaHost(EffectWorld world)
        {
            _world = world;
        }

        public LuaRunResult Run(string code)
        {
            List<string> logs = [];
            _world.Clear();

            try
            {
                Script script = new(CoreModules.Preset_SoftSandbox);
                script.Options.DebugPrint = message => logs.Add(message);
                script.Globals["effect"] = new LuaEffectApi(_world, logs);
                script.Globals["scene"] = new LuaSceneApi(_world, logs);

                script.DoString(code);

                DynValue update = script.Globals.Get("update");
                if (update.Type == DataType.Function)
                {
                    script.Call(update, 1.0 / 60.0);
                }

                return new LuaRunResult(true, logs, _world.Objects.ToList());
            }
            catch (ScriptRuntimeException ex)
            {
                logs.Add(ex.DecoratedMessage);
                return new LuaRunResult(false, logs, _world.Objects.ToList());
            }
            catch (SyntaxErrorException ex)
            {
                logs.Add(ex.DecoratedMessage);
                return new LuaRunResult(false, logs, _world.Objects.ToList());
            }
        }
    }
}
