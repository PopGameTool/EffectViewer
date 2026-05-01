using System;
using EffectViewer.TodLib.Particle;

namespace EffectViewer.Runtime.Showcase
{
    public sealed class ShowcaseParticleInstance
    {
        internal ShowcaseParticleInstance(TodParticle particle)
        {
            Particle = particle;
        }

        internal TodParticle Particle { get; }

        public int duration
        {
            get => Particle?.mParticleDuration ?? 0;
            set
            {
                if (Particle is not null)
                {
                    Particle.mParticleDuration = Math.Max(1, value);
                }
            }
        }

        public int age
        {
            get => Particle?.mParticleAge ?? 0;
            set
            {
                if (Particle is not null)
                {
                    Particle.mParticleAge = Math.Max(0, value);
                }
            }
        }

        public double time
        {
            get => Particle?.mParticleTimeValue ?? 0;
            set
            {
                if (Particle is not null)
                {
                    Particle.mParticleTimeValue = (float)value;
                }
            }
        }

        public double last_time
        {
            get => Particle?.mParticleLastTimeValue ?? 0;
            set
            {
                if (Particle is not null)
                {
                    Particle.mParticleLastTimeValue = (float)value;
                }
            }
        }

        public double animation_time
        {
            get => Particle?.mAnimationTimeValue ?? 0;
            set
            {
                if (Particle is not null)
                {
                    Particle.mAnimationTimeValue = (float)value;
                }
            }
        }

        public double x
        {
            get => Particle?.mPosition.X ?? 0;
            set
            {
                if (Particle is not null)
                {
                    Particle.mPosition.X = (float)value;
                }
            }
        }

        public double y
        {
            get => Particle?.mPosition.Y ?? 0;
            set
            {
                if (Particle is not null)
                {
                    Particle.mPosition.Y = (float)value;
                }
            }
        }

        public double velocity_x
        {
            get => Particle?.mVelocity.X ?? 0;
            set
            {
                if (Particle is not null)
                {
                    Particle.mVelocity.X = (float)value;
                }
            }
        }

        public double velocity_y
        {
            get => Particle?.mVelocity.Y ?? 0;
            set
            {
                if (Particle is not null)
                {
                    Particle.mVelocity.Y = (float)value;
                }
            }
        }

        public int image_frame
        {
            get => Particle?.mImageFrame ?? 0;
            set
            {
                if (Particle is not null)
                {
                    Particle.mImageFrame = value;
                }
            }
        }

        public double spin
        {
            get => Particle?.mSpinPosition ?? 0;
            set
            {
                if (Particle is not null)
                {
                    Particle.mSpinPosition = (float)value;
                }
            }
        }

        public double spin_velocity
        {
            get => Particle?.mSpinVelocity ?? 0;
            set
            {
                if (Particle is not null)
                {
                    Particle.mSpinVelocity = (float)value;
                }
            }
        }

        public int cross_fade_duration
        {
            get => Particle?.mCrossFadeDuration ?? 0;
            set
            {
                if (Particle is not null)
                {
                    Particle.mCrossFadeDuration = Math.Max(0, value);
                }
            }
        }

        public ShowcaseParticleEmitter emitter()
        {
            return Particle?.mParticleEmitter is null ? null : new ShowcaseParticleEmitter(Particle.mParticleEmitter);
        }

        public ShowcaseParticleInstance set_position(double x, double y)
        {
            if (Particle is not null)
            {
                Particle.mPosition.X = (float)x;
                Particle.mPosition.Y = (float)y;
            }

            return this;
        }

        public ShowcaseParticleInstance set_velocity(double x, double y)
        {
            if (Particle is not null)
            {
                Particle.mVelocity.X = (float)x;
                Particle.mVelocity.Y = (float)y;
            }

            return this;
        }
    }
}
