using System;
using System.Runtime.CompilerServices;
using EffectViewer.Runtime.Lua;
using EffectViewer.EffectRuntime.Particle;
using MoonSharp.Interpreter;

namespace EffectViewer.Runtime.Showcase
{
    public sealed class ShowcaseParticleInstance
    {
        internal ShowcaseParticleInstance(ParticleInstance particle)
        {
            Particle = particle;
        }

        internal ParticleInstance Particle { get; }

        public ShowcaseParticleEmitter particle_emitter => emitter();

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

        public int particle_duration
        {
            get => duration;
            set => duration = value;
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

        public int particle_age
        {
            get => age;
            set => age = value;
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

        public double particle_time_value
        {
            get => time;
            set => time = value;
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

        public double particle_last_time_value
        {
            get => last_time;
            set => last_time = value;
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

        public double animation_time_value
        {
            get => animation_time;
            set => animation_time = value;
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

        public double position_x
        {
            get => x;
            set => x = value;
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

        public double position_y
        {
            get => y;
            set => y = value;
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

        public double spin_position
        {
            get => spin;
            set => spin = value;
        }

        public double cross_fade_particle_id
        {
            get => Particle is null ? 0 : IdToNumber(Particle.mCrossFadeParticleID);
            set
            {
                if (Particle is not null)
                {
                    Particle.mCrossFadeParticleID = IdFromNumber<ParticleID>(value);
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

        public double pos_x() => Particle?.mPosition.X ?? 0;
        public double pos_y() => Particle?.mPosition.Y ?? 0;
        public double velocity_x_value() => Particle?.mVelocity.X ?? 0;
        public double velocity_y_value() => Particle?.mVelocity.Y ?? 0;
        public bool is_cross_fading() => Particle is not null && Particle.mCrossFadeDuration > 0;

        public ShowcaseParticleEmitter emitter()
        {
            return Particle?.mParticleEmitter is null ? null : new ShowcaseParticleEmitter(Particle.mParticleEmitter);
        }

        public double get_particle_interp(int index)
        {
            return Particle is not null && index >= 0 && index < (int)ParticleTracks.NumParticleTracks
                ? Particle.mParticleInterp[index]
                : 0d;
        }

        public ShowcaseParticleInstance set_particle_interp(int index, double value)
        {
            if (Particle is not null && index >= 0 && index < (int)ParticleTracks.NumParticleTracks)
            {
                Particle.mParticleInterp[index] = (float)value;
            }

            return this;
        }

        public DynValue get_particle_field_interp(int index)
        {
            if (Particle is null || index < 0 || index >= 4)
            {
                return DynValue.Nil;
            }

            return DynValue.NewTuple(
                DynValue.NewNumber(Particle.mParticleFieldInterp[index][0]),
                DynValue.NewNumber(Particle.mParticleFieldInterp[index][1]));
        }

        public ShowcaseParticleInstance set_particle_field_interp(int index, double value1, double value2)
        {
            if (Particle is not null && index >= 0 && index < 4)
            {
                Particle.mParticleFieldInterp[index][0] = (float)value1;
                Particle.mParticleFieldInterp[index][1] = (float)value2;
            }

            return this;
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

        public ShowcaseParticleInstance offset_velocity(double x, double y)
        {
            if (Particle is not null)
            {
                Particle.mVelocity.X += (float)x;
                Particle.mVelocity.Y += (float)y;
            }

            return this;
        }

        public ShowcaseParticleInstance set_age(double value)
        {
            age = (int)Math.Round(value);
            return this;
        }

        public ShowcaseParticleInstance set_duration(double value)
        {
            duration = (int)Math.Round(value);
            return this;
        }

        public ShowcaseParticleInstance move(double x, double y)
        {
            return set_position(x, y);
        }

        public ShowcaseParticleInstance offset(double x, double y)
        {
            if (Particle is not null)
            {
                Particle.mPosition.X += (float)x;
                Particle.mPosition.Y += (float)y;
            }

            return this;
        }

        private static double IdToNumber<TId>(TId id)
            where TId : unmanaged
        {
            uint raw = Unsafe.As<TId, uint>(ref id);
            return raw;
        }

        private static TId IdFromNumber<TId>(double id)
            where TId : unmanaged
        {
            uint raw = double.IsFinite(id) && id > 0 ? unchecked((uint)Math.Round(id)) : 0U;
            return Unsafe.As<uint, TId>(ref raw);
        }
    }
}
