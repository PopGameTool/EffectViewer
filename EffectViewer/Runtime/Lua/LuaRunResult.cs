using System.Collections.Generic;
using EffectViewer.Rendering;

namespace EffectViewer.Runtime.Lua
{
    public sealed class LuaRunResult
    {
        public bool Success { get; }
        public IReadOnlyList<string> Logs { get; }
        public IReadOnlyList<SceneObject> SceneObjects { get; }
        public IRenderFrameProvider FrameProvider { get; }

        public LuaRunResult(
            bool success,
            IReadOnlyList<string> logs,
            IReadOnlyList<SceneObject> sceneObjects,
            IRenderFrameProvider frameProvider)
        {
            Success = success;
            Logs = logs;
            SceneObjects = sceneObjects;
            FrameProvider = frameProvider;
        }
    }
}
