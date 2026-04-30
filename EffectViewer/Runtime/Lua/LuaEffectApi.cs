using System.Collections.Generic;
using EffectViewer.Projects;

namespace EffectViewer.Runtime.Lua
{
    public sealed class LuaEffectApi
    {
        private readonly EffectWorld _world;
        private readonly IList<string> _logs;

        public LuaEffectApi(EffectWorld world, IList<string> logs)
        {
            _world = world;
            _logs = logs;
        }

        public SceneObject reanim(string id, double x, double y)
        {
            _logs.Add($"reanim: {id} at {x:0.##}, {y:0.##}");
            return _world.AddObject(EffectAssetKind.Reanim, id, x, y);
        }

        public SceneObject particle(string id, double x, double y)
        {
            _logs.Add($"particle: {id} at {x:0.##}, {y:0.##}");
            return _world.AddObject(EffectAssetKind.Particle, id, x, y);
        }

        public SceneObject trail(string id)
        {
            _logs.Add($"trail: {id}");
            return _world.AddObject(EffectAssetKind.Trail, id, 0, 0);
        }

        public void log(string message)
        {
            _logs.Add(message);
        }
    }
}
