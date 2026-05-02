using System.Collections.Generic;
using System.Linq;
using EffectViewer.Projects;
using EffectViewer.Runtime.Showcase;
using EffectViewer.TodLib.Common;

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

        public ShowcaseVector vector(double x, double y)
        {
            return new ShowcaseVector(x, y);
        }

        public ShowcaseMatrix matrix(double m11, double m12, double m21, double m22, double x, double y)
        {
            return new ShowcaseMatrix(m11, m12, m21, m22, x, y);
        }

        public bool reanim_exists(string id)
        {
            return !string.IsNullOrWhiteSpace(id) && _world.Project?.Assets.Reanims.ContainsKey(id) == true;
        }

        public bool particle_exists(string id)
        {
            return !string.IsNullOrWhiteSpace(id) && _world.Project?.Assets.Particles.ContainsKey(id) == true;
        }

        public bool trail_exists(string id)
        {
            return !string.IsNullOrWhiteSpace(id) && _world.Project?.Assets.Trails.ContainsKey(id) == true;
        }

        public bool image_exists(string id)
        {
            return !string.IsNullOrWhiteSpace(id) && _world.Project?.Assets.TryGetImage(id, out _) == true;
        }

        public ShowcaseImage image(string id)
        {
            Image image = ResourceHandler.GetImage(id);
            return image is null ? null : new ShowcaseImage(image);
        }

        public int reanim_count()
        {
            return _world.Project?.Assets.Reanims.Count ?? 0;
        }

        public int particle_count()
        {
            return _world.Project?.Assets.Particles.Count ?? 0;
        }

        public int trail_count()
        {
            return _world.Project?.Assets.Trails.Count ?? 0;
        }

        public int image_count()
        {
            return _world.Project?.Assets.Images.Count ?? 0;
        }

        public string reanim_id(int index)
        {
            return IdAt(_world.Project?.Assets.Reanims, index);
        }

        public string particle_id(int index)
        {
            return IdAt(_world.Project?.Assets.Particles, index);
        }

        public string trail_id(int index)
        {
            return IdAt(_world.Project?.Assets.Trails, index);
        }

        public string image_id(int index)
        {
            return IdAt(_world.Project?.Assets.Images, index);
        }

        private static string IdAt<TAsset>(IReadOnlyDictionary<string, TAsset> assets, int index)
        {
            if (assets is null || index < 0 || index >= assets.Count)
            {
                return null;
            }

            return assets.Keys
                .OrderBy(static id => id, System.StringComparer.OrdinalIgnoreCase)
                .Skip(index)
                .FirstOrDefault();
        }
    }
}
