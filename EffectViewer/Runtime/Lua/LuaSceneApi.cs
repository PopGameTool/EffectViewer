using System.Collections.Generic;
using System;
using EffectViewer.Runtime.Showcase;

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

        public int count()
        {
            return _world.Objects.Count;
        }

        public SceneObject object_at(int index)
        {
            return index < 0 || index >= _world.Objects.Count ? null : _world.Objects[index];
        }

        public ShowcaseReanimation reanim_at(int index)
        {
            IReadOnlyList<ShowcaseReanimation> reanimations = _world.ShowcaseScene?.Reanimations;
            return reanimations is null || index < 0 || index >= reanimations.Count ? null : reanimations[index];
        }

        public ShowcaseReanimation find_reanim(string id)
        {
            IReadOnlyList<ShowcaseReanimation> reanimations = _world.ShowcaseScene?.Reanimations;
            if (reanimations is null || string.IsNullOrWhiteSpace(id))
            {
                return null;
            }

            foreach (ShowcaseReanimation reanimation in reanimations)
            {
                if (string.Equals(reanimation.id_, id, StringComparison.OrdinalIgnoreCase))
                {
                    return reanimation;
                }
            }

            return null;
        }

        public ShowcaseParticle particle_at(int index)
        {
            IReadOnlyList<ShowcaseParticle> particles = _world.ShowcaseScene?.Particles;
            return particles is null || index < 0 || index >= particles.Count ? null : particles[index];
        }

        public ShowcaseParticle find_particle(string id)
        {
            IReadOnlyList<ShowcaseParticle> particles = _world.ShowcaseScene?.Particles;
            if (particles is null || string.IsNullOrWhiteSpace(id))
            {
                return null;
            }

            foreach (ShowcaseParticle particle in particles)
            {
                if (string.Equals(particle.id_, id, StringComparison.OrdinalIgnoreCase))
                {
                    return particle;
                }
            }

            return null;
        }

        public ShowcaseTrail trail_at(int index)
        {
            IReadOnlyList<ShowcaseTrail> trails = _world.ShowcaseScene?.Trails;
            return trails is null || index < 0 || index >= trails.Count ? null : trails[index];
        }

        public ShowcaseTrail find_trail(string id)
        {
            IReadOnlyList<ShowcaseTrail> trails = _world.ShowcaseScene?.Trails;
            if (trails is null || string.IsNullOrWhiteSpace(id))
            {
                return null;
            }

            foreach (ShowcaseTrail trail in trails)
            {
                if (string.Equals(trail.id_, id, StringComparison.OrdinalIgnoreCase))
                {
                    return trail;
                }
            }

            return null;
        }

        public SceneObject find_object(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return null;
            }

            foreach (SceneObject sceneObject in _world.Objects)
            {
                if (string.Equals(sceneObject.Id, id, StringComparison.OrdinalIgnoreCase))
                {
                    return sceneObject;
                }
            }

            return null;
        }

        public int reanim_count()
        {
            return _world.ShowcaseScene?.Reanimations.Count ?? 0;
        }

        public int particle_count()
        {
            return _world.ShowcaseScene?.Particles.Count ?? 0;
        }

        public int trail_count()
        {
            return _world.ShowcaseScene?.Trails.Count ?? 0;
        }
    }
}
