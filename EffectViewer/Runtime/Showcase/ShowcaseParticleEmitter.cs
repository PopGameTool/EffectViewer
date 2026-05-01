using System;
using System.Collections.Generic;
using EffectViewer.Runtime.Lua;
using EffectViewer.TodLib.Graphics;
using EffectViewer.TodLib.Particle;

namespace EffectViewer.Runtime.Showcase
{
    public sealed class ShowcaseParticleEmitter
    {
        internal ShowcaseParticleEmitter(TodParticleEmitter emitter)
        {
            Emitter = emitter;
        }

        internal TodParticleEmitter Emitter { get; }

        public string name => Emitter?.mEmitterDef?.mName ?? string.Empty;

        public double spawn_accum
        {
            get => Emitter?.mSpawnAccum ?? 0;
            set
            {
                if (Emitter is not null)
                {
                    Emitter.mSpawnAccum = (float)value;
                }
            }
        }

        public double x
        {
            get => Emitter?.mSystemCenter.X ?? 0;
            set
            {
                if (Emitter is not null)
                {
                    Emitter.SystemMove((float)value, Emitter.mSystemCenter.Y);
                }
            }
        }

        public double y
        {
            get => Emitter?.mSystemCenter.Y ?? 0;
            set
            {
                if (Emitter is not null)
                {
                    Emitter.SystemMove(Emitter.mSystemCenter.X, (float)value);
                }
            }
        }

        public int particles_spawned
        {
            get => Emitter?.mParticlesSpawned ?? 0;
            set
            {
                if (Emitter is not null)
                {
                    Emitter.mParticlesSpawned = Math.Max(0, value);
                }
            }
        }

        public int system_age
        {
            get => Emitter?.mSystemAge ?? 0;
            set
            {
                if (Emitter is not null)
                {
                    Emitter.mSystemAge = value;
                }
            }
        }

        public int system_duration
        {
            get => Emitter?.mSystemDuration ?? 0;
            set
            {
                if (Emitter is not null)
                {
                    Emitter.mSystemDuration = Math.Max(1, value);
                }
            }
        }

        public double system_time
        {
            get => Emitter?.mSystemTimeValue ?? 0;
            set
            {
                if (Emitter is not null)
                {
                    Emitter.mSystemTimeValue = (float)value;
                }
            }
        }

        public double last_system_time
        {
            get => Emitter?.mSystemLastTimeValue ?? 0;
            set
            {
                if (Emitter is not null)
                {
                    Emitter.mSystemLastTimeValue = (float)value;
                }
            }
        }

        public bool dead
        {
            get => Emitter?.mDead ?? true;
            set
            {
                if (Emitter is not null)
                {
                    Emitter.mDead = value;
                }
            }
        }

        public bool extra_additive_draw
        {
            get => Emitter?.mExtraAdditiveDrawOverride ?? false;
            set
            {
                if (Emitter is not null)
                {
                    Emitter.mExtraAdditiveDrawOverride = value;
                }
            }
        }

        public double scale_override
        {
            get => Emitter?.mScaleOverride ?? 1;
            set
            {
                if (Emitter is not null)
                {
                    Emitter.mScaleOverride = (float)value;
                }
            }
        }

        public int frame
        {
            get => Emitter?.mFrameOverride ?? -1;
            set
            {
                if (Emitter is not null)
                {
                    Emitter.mFrameOverride = value;
                }
            }
        }

        public int particle_count => Emitter?.mParticleList.Count ?? 0;

        public bool is_dead() => Emitter is null || Emitter.mDead;

        public ShowcaseParticleEmitter update()
        {
            Emitter?.Update();
            return this;
        }

        public ShowcaseParticleEmitter draw(LuaGraphicsApi graphics)
        {
            if (graphics is not null && Emitter is { mDead: false })
            {
                Emitter.Draw(graphics.Graphics);
            }

            return this;
        }

        public ShowcaseParticleEmitter set_position(double x, double y)
        {
            Emitter?.SystemMove((float)x, (float)y);
            return this;
        }

        public ShowcaseParticleEmitter set_color(double red, double green, double blue)
        {
            return set_color(red, green, blue, 255);
        }

        public ShowcaseParticleEmitter set_color(double red, double green, double blue, double alpha)
        {
            if (Emitter is not null)
            {
                Emitter.mColorOverride = new SexyColor(
                    ClampColor(red),
                    ClampColor(green),
                    ClampColor(blue),
                    ClampColor(alpha));
            }

            return this;
        }

        public ShowcaseParticleEmitter set_scale(double scale)
        {
            if (Emitter is not null)
            {
                Emitter.mScaleOverride = (float)scale;
            }

            return this;
        }

        public ShowcaseParticleEmitter set_frame(double frame)
        {
            if (Emitter is not null)
            {
                Emitter.mFrameOverride = (int)Math.Round(frame);
            }

            return this;
        }

        public ShowcaseParticleEmitter delete_all()
        {
            Emitter?.DeleteAll();
            return this;
        }

        public ShowcaseParticleEmitter delete_non_cross_fading()
        {
            Emitter?.DeleteNonCrossFading();
            return this;
        }

        public bool cross_fade_particle(ShowcaseParticleInstance particle, ShowcaseParticleEmitter toEmitter)
        {
            if (Emitter is null || particle?.Particle is null || toEmitter?.Emitter is null)
            {
                return false;
            }

            return Emitter.CrossFadeParticle(particle.Particle, toEmitter.Emitter);
        }

        public bool cross_fade_particle_to_name(ShowcaseParticleInstance particle, string emitterName)
        {
            return Emitter is not null &&
                particle?.Particle is not null &&
                Emitter.CrossFadeParticleToName(particle.Particle, emitterName);
        }

        public ShowcaseParticleEmitter cross_fade_to(ShowcaseParticleEmitter toEmitter)
        {
            if (Emitter is not null && toEmitter?.Emitter is not null)
            {
                Emitter.CrossFadeEmitter(toEmitter.Emitter);
            }

            return this;
        }

        public ShowcaseParticleInstance particle(int index)
        {
            if (Emitter is null || index < 0 || index >= Emitter.mParticleList.Count)
            {
                return null;
            }

            int current = 0;
            for (LinkedListNode<ParticleID> node = Emitter.mParticleList.First; node is not null; node = node.Next)
            {
                if (current++ == index)
                {
                    TodParticle particle = Emitter.mParticleSystem.mParticleHolder.mParticles.DataArrayTryToGet(node.Value);
                    return particle is null ? null : new ShowcaseParticleInstance(particle);
                }
            }

            return null;
        }

        private static int ClampColor(double value)
        {
            return Math.Clamp((int)Math.Round(value), 0, 255);
        }
    }
}
