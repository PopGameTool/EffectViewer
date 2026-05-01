using System.Collections.Generic;
using EffectViewer.Projects;
using EffectViewer.Runtime.Showcase;

namespace EffectViewer.Runtime.Lua
{
    public sealed class LuaEffectApi
    {
        private readonly EffectWorld _world;
        private readonly ShowcaseScene _scene;
        private readonly IList<string> _logs;

        public LuaEffectApi(EffectWorld world, ShowcaseScene scene, IList<string> logs)
        {
            _world = world;
            _scene = scene;
            _logs = logs;
        }

        public ShowcaseReanimation reanim(string id, double x, double y)
        {
            _logs.Add($"reanim: {id} at {x:0.##}, {y:0.##}");
            _world.AddObject(EffectAssetKind.Reanim, id, x, y);
            return _scene.AddReanimation(id, x, y);
        }

        public ShowcaseParticle particle(string id, double x, double y)
        {
            _logs.Add($"particle: {id} at {x:0.##}, {y:0.##}");
            _world.AddObject(EffectAssetKind.Particle, id, x, y);
            return _scene.AddParticle(id, x, y);
        }

        public ShowcaseTrail trail(string id)
        {
            _logs.Add($"trail: {id}");
            _world.AddObject(EffectAssetKind.Trail, id, 0, 0);
            return _scene.AddTrail(id, 0, 0);
        }

        public ShowcaseTrail trail(string id, double x, double y)
        {
            _logs.Add($"trail: {id} at {x:0.##}, {y:0.##}");
            _world.AddObject(EffectAssetKind.Trail, id, x, y);
            return _scene.AddTrail(id, x, y);
        }

        public void log(string message)
        {
            _logs.Add(message);
        }

        public void warn(string message)
        {
            _logs.Add($"warning: {message}");
        }
    }
}
