using System.Collections.Generic;

namespace EffectViewer.Runtime.Lua
{
    public sealed class LuaRunResult
    {
        public bool Success { get; }
        public IReadOnlyList<string> Logs { get; }
        public IReadOnlyList<SceneObject> SceneObjects { get; }

        public LuaRunResult(bool success, IReadOnlyList<string> logs, IReadOnlyList<SceneObject> sceneObjects)
        {
            Success = success;
            Logs = logs;
            SceneObjects = sceneObjects;
        }
    }
}
