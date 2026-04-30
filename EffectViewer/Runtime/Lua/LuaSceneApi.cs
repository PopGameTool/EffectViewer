using System.Collections.Generic;

namespace EffectViewer.Runtime.Lua
{
    public sealed class LuaSceneApi
    {
        private readonly EffectWorld _world;
        private readonly IList<string> _logs;

        public LuaSceneApi(EffectWorld world, IList<string> logs)
        {
            _world = world;
            _logs = logs;
        }

        public void clear()
        {
            _world.Clear();
            _logs.Add("scene cleared");
        }
    }
}
