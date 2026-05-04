using System;
using System.Collections.Generic;
using System.Linq;
using EffectViewer.Projects;
using EffectViewer.Runtime.Showcase;
using EffectViewer.TodLib.Common;
using EffectViewer.TodLib.Graphics;
using EffectViewer.TodLib.Particle;
using EffectViewer.TodLib.Reanim;
using EffectViewer.TodLib.Reanim.Attachment;
using EffectViewer.TodLib.Trail;
using MoonSharp.Interpreter;

namespace EffectViewer.Runtime.Lua
{
    public sealed class LuaSceneApi
    {
        private readonly EffectWorld _world;
        private readonly ShowcaseScene _scene;
        private readonly IList<string> _logs;
        private readonly Dictionary<string, ShowcaseImage> _imageCache = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, ShowcaseFont> _fontCache = new(StringComparer.OrdinalIgnoreCase);

        public LuaSceneApi(EffectWorld world, ShowcaseScene scene, IList<string> logs)
        {
            _world = world;
            _scene = scene;
            _logs = logs;
            global_attachment = new LuaAttachmentApi(scene);
        }

        internal bool HasRegisteredContext { get; private set; }
        internal DynValue RegisteredContext { get; private set; } = DynValue.Nil;

        public LuaAttachmentApi global_attachment { get; }

        public void regist(DynValue context)
        {
            if (context.Type is DataType.Nil or DataType.Void)
            {
                HasRegisteredContext = false;
                RegisteredContext = DynValue.Nil;
                return;
            }

            if (context.Type != DataType.Table)
            {
                throw new ScriptRuntimeException("scene.regist(context) expects a Lua table.");
            }

            HasRegisteredContext = true;
            RegisteredContext = context;
        }

        public void clear()
        {
            _world.Clear();
            _logs.Add("scene cleared");
        }

        public ShowcaseReanimation reanim(string id, double x, double y)
        {
            _logs.Add($"reanim: {id} at {x:0.##}, {y:0.##}");
            _world.AddObject(EffectAssetKind.Reanim, id, x, y);
            return _scene.AddReanimation(id, x, y);
        }

        public ShowcaseParticle particle_system(string id, double x, double y)
        {
            _logs.Add($"particle: {id} at {x:0.##}, {y:0.##}");
            _world.AddObject(EffectAssetKind.Particle, id, x, y);
            return _scene.AddParticle(id, x, y);
        }

        public ShowcaseTrail trail(string id, double x, double y)
        {
            _logs.Add($"trail: {id} at {x:0.##}, {y:0.##}");
            _world.AddObject(EffectAssetKind.Trail, id, x, y);
            return _scene.AddTrail(id, x, y);
        }

        public void log(string message)
        {
            _logs.Add(message ?? string.Empty);
        }

        public void warn(string message)
        {
            _logs.Add($"warning: {message}");
        }

        public ShowcaseVector vector2(double x, double y)
        {
            return new ShowcaseVector(x, y);
        }

        public ShowcaseVector3 vector3(double x, double y, double z)
        {
            return new ShowcaseVector3(x, y, z);
        }

        public ShowcaseMatrix matrix3x3()
        {
            return ShowcaseMatrix.Identity();
        }

        public ShowcaseMatrix matrix3x3(
            double m11,
            double m12,
            double m13,
            double m21,
            double m22,
            double m23,
            double m31,
            double m32,
            double m33)
        {
            return new ShowcaseMatrix(m11, m12, m13, m21, m22, m23, m31, m32, m33);
        }

        public ShowcaseImage get_image(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return null;
            }

            if (_imageCache.TryGetValue(id, out ShowcaseImage cached))
            {
                return cached;
            }

            Image image = ResourceHandler.GetImage(id);
            if (image is null)
            {
                return null;
            }

            ShowcaseImage result = new(image);
            _imageCache[id] = result;
            return result;
        }

        public ShowcaseFont get_font(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return null;
            }

            if (_fontCache.TryGetValue(id, out ShowcaseFont cached))
            {
                return cached;
            }

            Font font = ResourceHandler.GetFont(id);
            if (font is null)
            {
                return null;
            }

            ShowcaseFont result = new(font);
            _fontCache[id] = result;
            return result;
        }

        public bool resource_exist(string id, string type)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return false;
            }

            string normalized = (type ?? string.Empty).Trim().ToLowerInvariant();
            return normalized switch
            {
                "reanim" or "reanimation" => _world.Project?.Assets.Reanims.ContainsKey(id) == true,
                "particle" or "particle_system" or "particles" => _world.Project?.Assets.Particles.ContainsKey(id) == true,
                "trail" => _world.Project?.Assets.Trails.ContainsKey(id) == true,
                "image" or "img" => _world.Project?.Assets.TryGetImage(id, out _) == true,
                "font" => _world.Project?.Assets.TryGetFont(id, out _) == true,
                _ => false
            };
        }

        public ShowcaseTriVertex tri_vertex(
            double pos_x,
            double pos_y,
            double pos_z,
            double red,
            double green,
            double blue,
            double alpha,
            double coordinate_x,
            double coordinate_y)
        {
            return new ShowcaseTriVertex(pos_x, pos_y, pos_z, red, green, blue, alpha, coordinate_x, coordinate_y);
        }

        public double reanim_get_id(ShowcaseReanimation reanim)
        {
            return _scene.GetReanimationId(reanim);
        }

        public double particle_system_get_id(ShowcaseParticle particle)
        {
            return _scene.GetParticleSystemId(particle);
        }

        public double particle_get_id(ShowcaseParticle particle)
        {
            return particle_system_get_id(particle);
        }

        public double emitter_get_id(ShowcaseParticleEmitter emitter)
        {
            return _scene.GetEmitterId(emitter);
        }

        public double particle_get_id(ShowcaseParticleInstance particle)
        {
            return _scene.GetParticleId(particle);
        }

        public double attachment_get_id(ShowcaseAttachment attachment)
        {
            return _scene.GetAttachmentId(attachment);
        }

        public double trail_get_id(ShowcaseTrail trail)
        {
            return _scene.GetTrailId(trail);
        }

        public ShowcaseReanimation reanim_get(double id)
        {
            return reanim_try_to_get(id);
        }

        public ShowcaseParticle particle_system_get(double id)
        {
            return particle_system_try_to_get(id);
        }

        public ShowcaseParticleEmitter emitter_get(double id)
        {
            return emitter_try_to_get(id);
        }

        public ShowcaseParticleInstance particle_get(double id)
        {
            return particle_try_to_get(id);
        }

        public ShowcaseParticleInstance particle_instance_get(double id)
        {
            return particle_get(id);
        }

        public ShowcaseAttachment attachment_get(double id)
        {
            return attachment_try_to_get(id);
        }

        public ShowcaseTrail trail_get(double id)
        {
            return trail_try_to_get(id);
        }

        public ShowcaseReanimation reanim_try_to_get(double id)
        {
            Reanimation reanimation = _scene.GetReanimationById(id);
            return reanimation is null ? null : new ShowcaseReanimation(_scene, reanimation.mReanimationType, reanimation);
        }

        public ShowcaseParticle particle_system_try_to_get(double id)
        {
            TodParticleSystem particle = _scene.GetParticleSystemById(id);
            return particle is null ? null : new ShowcaseParticle(_scene, particle.mEffectType, particle);
        }

        public ShowcaseParticleEmitter emitter_try_to_get(double id)
        {
            TodParticleEmitter emitter = _scene.GetEmitterById(id);
            return emitter is null ? null : new ShowcaseParticleEmitter(emitter);
        }

        public ShowcaseParticleInstance particle_try_to_get(double id)
        {
            TodParticle particle = _scene.GetParticleById(id);
            return particle is null ? null : new ShowcaseParticleInstance(particle);
        }

        public ShowcaseParticleInstance particle_instance_try_to_get(double id)
        {
            return particle_try_to_get(id);
        }

        public ShowcaseAttachment attachment_try_to_get(double id)
        {
            Attachment attachment = _scene.GetAttachmentById(id);
            return attachment is null ? null : new ShowcaseAttachment(attachment);
        }

        public ShowcaseTrail trail_try_to_get(double id)
        {
            Trail trail = _scene.GetTrailById(id);
            return trail is null ? null : new ShowcaseTrail(_scene, trail.mDefinition?.mImage, trail, trail.mTrailCenter.X, trail.mTrailCenter.Y);
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
            return _world.ShowcaseScene?.Reanimations.FirstOrDefault(
                reanimation => string.Equals(reanimation.id_, id, StringComparison.OrdinalIgnoreCase));
        }

        public ShowcaseParticle particle_at(int index)
        {
            IReadOnlyList<ShowcaseParticle> particles = _world.ShowcaseScene?.Particles;
            return particles is null || index < 0 || index >= particles.Count ? null : particles[index];
        }

        public ShowcaseParticle find_particle(string id)
        {
            return _world.ShowcaseScene?.Particles.FirstOrDefault(
                particle => string.Equals(particle.id_, id, StringComparison.OrdinalIgnoreCase));
        }

        public ShowcaseTrail trail_at(int index)
        {
            IReadOnlyList<ShowcaseTrail> trails = _world.ShowcaseScene?.Trails;
            return trails is null || index < 0 || index >= trails.Count ? null : trails[index];
        }

        public ShowcaseTrail find_trail(string id)
        {
            return _world.ShowcaseScene?.Trails.FirstOrDefault(
                trail => string.Equals(trail.id_, id, StringComparison.OrdinalIgnoreCase));
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
