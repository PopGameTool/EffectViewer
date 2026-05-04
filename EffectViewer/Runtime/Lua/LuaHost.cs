using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
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
            LuaApiRegistration.RegisterAll();
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
                LuaSceneApi sceneApi = new(_world, scene, logs);
                script.Globals["scene"] = sceneApi;
                script.Globals["global_attachment"] = sceneApi.global_attachment;

                script.DoString(code);

                bool hasContext = sceneApi.HasRegisteredContext;
                DynValue context = hasContext ? sceneApi.RegisteredContext : DynValue.Nil;
                DynValue update = hasContext ? GetContextFunction(context, "update") : DynValue.Nil;
                DynValue draw = hasContext ? GetContextFunction(context, "draw") : DynValue.Nil;
                ValidateOptionalFunction(update, "update");
                ValidateOptionalFunction(draw, "draw");

                LuaShowcaseScript callbacks = null;
                if (update.Type == DataType.Function || draw.Type == DataType.Function)
                {
                    callbacks = new LuaShowcaseScript(script, hasContext, context, update, draw, logs);
                    scene.SetScriptCallbacks(callbacks);
                }

                return new LuaRunResult(true, logs.ToList(), _world.Objects.ToList(), scene);
            }
            catch (ScriptRuntimeException ex)
            {
                logs.Add(ex.DecoratedMessage ?? ex.Message);
                scene.Dispose();
                return new LuaRunResult(false, logs.ToList(), _world.Objects.ToList(), null);
            }
            catch (SyntaxErrorException ex)
            {
                logs.Add(ex.DecoratedMessage ?? ex.Message);
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

        private static DynValue GetContextFunction(DynValue context, string name)
        {
            if (context.Type == DataType.Table)
            {
                return context.Table.Get(name);
            }

            throw new ScriptRuntimeException("scene.regist(context) expects a Lua table.");
        }

        internal static void RegisterLuaType<
            [DynamicallyAccessedMembers(
                DynamicallyAccessedMemberTypes.PublicConstructors |
                DynamicallyAccessedMemberTypes.PublicMethods |
                DynamicallyAccessedMemberTypes.PublicProperties |
                DynamicallyAccessedMemberTypes.PublicFields)]
            T>()
        {
            UserData.RegisterType<T>();
        }
    }
}
