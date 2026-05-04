using System.Collections.Generic;
using EffectViewer.Runtime.Lua;
using EffectViewer.TodLib.Common;
using EffectViewer.TodLib.Graphics;
using EffectViewer.TodLib.Particle;
using MoonSharp.Interpreter;

namespace EffectViewer.Runtime.Showcase
{
    public sealed class ShowcaseParticle
    {
        private readonly ShowcaseScene _scene;

        internal ShowcaseParticle(ShowcaseScene scene, string id, TodParticleSystem particleSystem)
        {
            _scene = scene;
            id_ = id;
            ParticleSystem = particleSystem;
        }

        internal TodParticleSystem ParticleSystem { get; }

        public string id_ { get; }
        public string type
        {
            get => ParticleSystem?.mEffectType;
            set
            {
                if (ParticleSystem is not null)
                {
                    ParticleSystem.mEffectType = value;
                }
            }
        }

        public string effect_type
        {
            get => type;
            set => type = value;
        }

        public bool dead
        {
            get => ParticleSystem?.mDead ?? true;
            set
            {
                if (ParticleSystem is not null)
                {
                    ParticleSystem.mDead = value;
                }
            }
        }

        public bool is_attachment
        {
            get => ParticleSystem?.mIsAttachment ?? false;
            set
            {
                if (ParticleSystem is not null)
                {
                    ParticleSystem.mIsAttachment = value;
                }
            }
        }

        public int render_order
        {
            get => ParticleSystem?.mRenderOrder ?? 0;
            set
            {
                if (ParticleSystem is not null)
                {
                    ParticleSystem.mRenderOrder = value;
                }
            }
        }

        public bool dont_update
        {
            get => ParticleSystem?.mDontUpdate ?? false;
            set
            {
                if (ParticleSystem is not null)
                {
                    ParticleSystem.mDontUpdate = value;
                }
            }
        }

        public int emitter_count => ParticleSystem?.mEmitterList.Count ?? 0;
        public int emitter_definition_count => ParticleSystem?.mParticleDef?.mEmitterDefCount ?? 0;
        public bool is_dead() => ParticleSystem is null || ParticleSystem.mDead;

        public double get_x()
        {
            TodParticleEmitter emitter = FirstEmitter();
            return emitter?.mSystemCenter.X ?? 0;
        }

        public double get_y()
        {
            TodParticleEmitter emitter = FirstEmitter();
            return emitter?.mSystemCenter.Y ?? 0;
        }

        public ShowcaseParticle set_position(double x, double y)
        {
            ParticleSystem?.SystemMove((float)x, (float)y);
            return this;
        }

        public double get_emitter_id(int index)
        {
            return _scene?.GetEmitterId(emitter_at(index)) ?? 0d;
        }

        public ShowcaseParticle tod_particle_initialize(double x, double y, string effectType)
        {
            type = effectType;
            set_position(x, y);
            return this;
        }

        public ShowcaseParticle move(double x, double y)
        {
            return set_position(x, y);
        }

        public ShowcaseParticle offset(double x, double y)
        {
            if (ParticleSystem is not null)
            {
                set_position(get_x() + x, get_y() + y);
            }

            return this;
        }

        public ShowcaseParticle set_color(double red, double green, double blue)
        {
            return set_color(red, green, blue, 255);
        }

        public ShowcaseParticle set_color(double red, double green, double blue, double alpha)
        {
            ParticleSystem?.OverrideColor(null, new SexyColor(
                ClampColor(red),
                ClampColor(green),
                ClampColor(blue),
                ClampColor(alpha)));
            return this;
        }

        public ShowcaseParticle override_color(DynValue emitterName, DynValue red, DynValue green, DynValue blue, DynValue alpha)
        {
            ParticleSystem?.OverrideColor(
                LuaApiUtility.StringOr(emitterName),
                new SexyColor(
                    LuaApiUtility.ClampColor(LuaApiUtility.NumberOr(red, 255)),
                    LuaApiUtility.ClampColor(LuaApiUtility.NumberOr(green, 255)),
                    LuaApiUtility.ClampColor(LuaApiUtility.NumberOr(blue, 255)),
                    LuaApiUtility.ClampColor(LuaApiUtility.NumberOr(alpha, 255))));
            return this;
        }

        public ShowcaseParticle set_emitter_color(string emitterName, double red, double green, double blue)
        {
            return set_emitter_color(emitterName, red, green, blue, 255);
        }

        public ShowcaseParticle set_emitter_color(string emitterName, double red, double green, double blue, double alpha)
        {
            ParticleSystem?.OverrideColor(NormalizeEmitterName(emitterName), new SexyColor(
                ClampColor(red),
                ClampColor(green),
                ClampColor(blue),
                ClampColor(alpha)));
            return this;
        }

        public ShowcaseParticle set_scale(double scale)
        {
            ParticleSystem?.OverrideScale(null, (float)scale);
            return this;
        }

        public ShowcaseParticle override_extra_additive_draw(DynValue emitterName, bool enabled)
        {
            ParticleSystem?.OverrideExtraAdditiveDraw(LuaApiUtility.StringOr(emitterName), enabled);
            return this;
        }

        public ShowcaseParticle override_image(DynValue emitterName, ShowcaseImage image)
        {
            ParticleSystem?.OverrideImage(LuaApiUtility.StringOr(emitterName), LuaApiUtility.ImageFrom(image));
            return this;
        }

        public ShowcaseParticle override_frame(DynValue emitterName, double frame)
        {
            ParticleSystem?.OverrideFrame(LuaApiUtility.StringOr(emitterName), (int)System.Math.Round(frame));
            return this;
        }

        public ShowcaseParticle override_scale(DynValue emitterName, double scale)
        {
            ParticleSystem?.OverrideScale(LuaApiUtility.StringOr(emitterName), (float)scale);
            return this;
        }

        public ShowcaseParticle set_emitter_scale(string emitterName, double scale)
        {
            ParticleSystem?.OverrideScale(NormalizeEmitterName(emitterName), (float)scale);
            return this;
        }

        public ShowcaseParticle set_frame(double frame)
        {
            ParticleSystem?.OverrideFrame(null, (int)System.Math.Round(frame));
            return this;
        }

        public ShowcaseParticle set_emitter_frame(string emitterName, double frame)
        {
            ParticleSystem?.OverrideFrame(NormalizeEmitterName(emitterName), (int)System.Math.Round(frame));
            return this;
        }

        public ShowcaseParticle set_image_override(string imageId)
        {
            ParticleSystem?.OverrideImage(null, RequireImage(imageId));
            return this;
        }

        public ShowcaseParticle clear_image_override()
        {
            ParticleSystem?.OverrideImage(null, null);
            return this;
        }

        public ShowcaseParticle set_emitter_image_override(string emitterName, string imageId)
        {
            ParticleSystem?.OverrideImage(NormalizeEmitterName(emitterName), RequireImage(imageId));
            return this;
        }

        public ShowcaseParticle clear_emitter_image_override(string emitterName)
        {
            ParticleSystem?.OverrideImage(NormalizeEmitterName(emitterName), null);
            return this;
        }

        public ShowcaseParticle set_extra_additive_draw(bool enabled)
        {
            ParticleSystem?.OverrideExtraAdditiveDraw(null, enabled);
            return this;
        }

        public ShowcaseParticle set_emitter_extra_additive_draw(string emitterName, bool enabled)
        {
            ParticleSystem?.OverrideExtraAdditiveDraw(NormalizeEmitterName(emitterName), enabled);
            return this;
        }

        public ShowcaseParticle cross_fade(string emitterName)
        {
            ParticleSystem?.CrossFade(emitterName);
            return this;
        }

        public ShowcaseParticleEmitter emitter(string emitterName)
        {
            TodParticleEmitter emitter = ParticleSystem?.FindEmitterByName(emitterName);
            return emitter is null ? null : new ShowcaseParticleEmitter(emitter);
        }

        public ShowcaseParticleEmitter find_emitter_by_name(string emitterName)
        {
            return emitter(emitterName);
        }

        public ShowcaseParticleEmitter emitter_at(int index)
        {
            if (ParticleSystem is null || index < 0 || index >= ParticleSystem.mEmitterList.Count)
            {
                return null;
            }

            int current = 0;
            for (LinkedListNode<ParticleEmitterID> node = ParticleSystem.mEmitterList.First; node is not null; node = node.Next)
            {
                if (current++ == index)
                {
                    TodParticleEmitter emitter = ParticleSystem.mParticleHolder.mEmitters.DataArrayTryToGet(node.Value);
                    return emitter is null ? null : new ShowcaseParticleEmitter(emitter);
                }
            }

            return null;
        }

        public string emitter_name(int index)
        {
            return emitter_at(index)?.name;
        }

        public bool emitter_exists(string emitterName)
        {
            return ParticleSystem?.FindEmitterByName(emitterName) is not null;
        }

        public int emitter_index(string emitterName)
        {
            if (ParticleSystem is null)
            {
                return -1;
            }

            int index = 0;
            for (LinkedListNode<ParticleEmitterID> node = ParticleSystem.mEmitterList.First; node is not null; node = node.Next)
            {
                TodParticleEmitter emitter = ParticleSystem.mParticleHolder.mEmitters.DataArrayTryToGet(node.Value);
                if (emitter is not null && string.Equals(emitter.mEmitterDef?.mName, emitterName, System.StringComparison.OrdinalIgnoreCase))
                {
                    return index;
                }

                index++;
            }

            return -1;
        }

        public string emitter_definition_name(int index)
        {
            TodEmitterDefinition definition = emitter_definition_at(index);
            return definition?.mName;
        }

        public string emitter_definition_image_id(int index)
        {
            TodEmitterDefinition definition = emitter_definition_at(index);
            return definition?.mImage;
        }

        public ShowcaseParticle delete_all()
        {
            if (ParticleSystem is not null)
            {
                foreach (ParticleEmitterID emitterId in ParticleSystem.mEmitterList)
                {
                    TodParticleEmitter emitter = ParticleSystem.mParticleHolder.mEmitters.DataArrayTryToGet(emitterId);
                    emitter?.DeleteAll();
                }
            }

            return this;
        }

        public ShowcaseParticle delete_emitter_particles(string emitterName)
        {
            ParticleSystem?.FindEmitterByName(emitterName)?.DeleteAll();
            return this;
        }

        public ShowcaseParticle update()
        {
            ParticleSystem?.Update();
            return this;
        }

        public ShowcaseParticle system_move(double x, double y)
        {
            return set_position(x, y);
        }

        public ShowcaseParticle draw(LuaGraphicsApi graphics)
        {
            if (graphics is not null && ParticleSystem is { mDead: false })
            {
                ParticleSystem.Draw(graphics.Graphics);
            }

            return this;
        }

        public ShowcaseParticle die()
        {
            ParticleSystem?.ParticleSystemDie();
            return this;
        }

        public ShowcaseParticle particle_system_die()
        {
            return die();
        }

        private TodParticleEmitter FirstEmitter()
        {
            if (ParticleSystem?.mEmitterList.First is null)
            {
                return null;
            }

            return ParticleSystem.mParticleHolder.mEmitters.DataArrayTryToGet(ParticleSystem.mEmitterList.First.Value);
        }

        private static string NormalizeEmitterName(string emitterName)
        {
            return string.IsNullOrWhiteSpace(emitterName) ? null : emitterName;
        }

        private static int ClampColor(double value)
        {
            return System.Math.Clamp((int)System.Math.Round(value), 0, 255);
        }

        private TodEmitterDefinition emitter_definition_at(int index)
        {
            return ParticleSystem?.mParticleDef?.mEmitterDefs is null ||
                index < 0 ||
                index >= ParticleSystem.mParticleDef.mEmitterDefCount
                ? null
                : ParticleSystem.mParticleDef.mEmitterDefs[index];
        }

        private static Image RequireImage(string imageId)
        {
            Image image = ResourceHandler.GetImage(imageId);
            return image ?? throw new System.InvalidOperationException($"Image '{imageId}' was not found in the current project.");
        }
    }
}
