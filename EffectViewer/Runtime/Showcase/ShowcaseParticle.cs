using System.Collections.Generic;
using EffectViewer.Runtime.Lua;
using EffectViewer.TodLib.Common;
using EffectViewer.TodLib.Particle;

namespace EffectViewer.Runtime.Showcase
{
    public sealed class ShowcaseParticle
    {
        internal ShowcaseParticle(ShowcaseScene scene, string id, TodParticleSystem particleSystem)
        {
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

        public ShowcaseParticle move(double x, double y)
        {
            return set_position(x, y);
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

        public ShowcaseParticle update()
        {
            ParticleSystem?.Update();
            return this;
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
    }
}
