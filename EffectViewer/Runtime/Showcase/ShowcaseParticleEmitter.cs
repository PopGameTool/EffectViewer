using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using EffectViewer.Runtime.Lua;
using EffectViewer.TodLib.Graphics;
using EffectViewer.TodLib.Particle;
using MoonSharp.Interpreter;

namespace EffectViewer.Runtime.Showcase
{
    public sealed class ShowcaseParticleEmitter
    {
        internal ShowcaseParticleEmitter(TodParticleEmitter emitter)
        {
            Emitter = emitter;
        }

        internal TodParticleEmitter Emitter { get; }

        public ShowcaseParticle particle_system => Emitter?.mParticleSystem is null
            ? null
            : new ShowcaseParticle(null, Emitter.mParticleSystem.mEffectType, Emitter.mParticleSystem);
        public string name => Emitter?.mEmitterDef?.mName ?? string.Empty;
        public string image_id => Emitter?.mEmitterDef?.mImage;
        public string image_override_id => Emitter?.mImageOverride?.mId;
        public ShowcaseImage image_override
        {
            get => Emitter?.mImageOverride is null ? null : new ShowcaseImage(Emitter.mImageOverride);
            set
            {
                if (Emitter is not null)
                {
                    Emitter.mImageOverride = LuaApiUtility.ImageFrom(value);
                }
            }
        }
        public int image_col => Emitter?.mEmitterDef?.mImageCol ?? 0;
        public int image_row => Emitter?.mEmitterDef?.mImageRow ?? 0;
        public int image_frames => Emitter?.mEmitterDef?.mImageFrames ?? 0;
        public bool animated => Emitter?.mEmitterDef?.mAnimated != 0;
        public string emitter_type => Emitter?.mEmitterDef?.mEmitterType.ToString();
        public string on_duration => Emitter?.mEmitterDef?.mOnDuration;
        public int particle_flags => Emitter?.mEmitterDef?.mParticleFlags ?? 0;
        public int particle_field_count => Emitter?.mEmitterDef?.mParticleFieldCount ?? 0;
        public int system_field_count => Emitter?.mEmitterDef?.mSystemFieldCount ?? 0;

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

        public double system_center_x
        {
            get => x;
            set => x = value;
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

        public double system_center_y
        {
            get => y;
            set => y = value;
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

        public double system_time_value
        {
            get => system_time;
            set => system_time = value;
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

        public double system_last_time_value
        {
            get => last_system_time;
            set => last_system_time = value;
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

        public bool extra_additive_draw_override
        {
            get => extra_additive_draw;
            set => extra_additive_draw = value;
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

        public int frame_override
        {
            get => frame;
            set => frame = value;
        }

        public int particle_count => Emitter?.mParticleList.Count ?? 0;
        public int cross_fade_countdown
        {
            get => Emitter?.mEmitterCrossFadeCountDown ?? 0;
            set
            {
                if (Emitter is not null)
                {
                    Emitter.mEmitterCrossFadeCountDown = Math.Max(0, value);
                }
            }
        }

        public double cross_fade_emitter_id
        {
            get => Emitter is null ? 0 : IdToNumber(Emitter.mCrossFadeEmitterID);
            set
            {
                if (Emitter is not null)
                {
                    Emitter.mCrossFadeEmitterID = IdFromNumber<ParticleEmitterID>(value);
                }
            }
        }

        public int emitter_cross_fade_count_down
        {
            get => cross_fade_countdown;
            set => cross_fade_countdown = value;
        }

        public bool is_dead() => Emitter is null || Emitter.mDead;
        public double center_x() => Emitter?.mSystemCenter.X ?? 0;
        public double center_y() => Emitter?.mSystemCenter.Y ?? 0;

        public ShowcaseParticleEmitter update()
        {
            Emitter?.Update();
            return this;
        }

        public double get_particle_id(int index)
        {
            ShowcaseParticleInstance instance = particle(index);
            return instance?.Particle is not null &&
                Emitter?.mParticleSystem?.mParticleHolder?.mParticles.DataArrayContains(instance.Particle) == true
                ? IdToNumber(Emitter.mParticleSystem.mParticleHolder.mParticles.DataArrayGetID(instance.Particle))
                : 0d;
        }

        public double get_track_interp(int index)
        {
            return Emitter is not null && index >= 0 && index < (int)ParticleSystemTracks.NumSystemTracks
                ? Emitter.mTrackInterp[index]
                : 0d;
        }

        public ShowcaseParticleEmitter set_track_interp(int index, double value)
        {
            if (Emitter is not null && index >= 0 && index < (int)ParticleSystemTracks.NumSystemTracks)
            {
                Emitter.mTrackInterp[index] = (float)value;
            }

            return this;
        }

        public DynValue get_system_field_interp(int index)
        {
            if (Emitter is null || index < 0 || index >= 4)
            {
                return DynValue.Nil;
            }

            return DynValue.NewTuple(
                DynValue.NewNumber(Emitter.mSystemFieldInterp[index][0]),
                DynValue.NewNumber(Emitter.mSystemFieldInterp[index][1]));
        }

        public ShowcaseParticleEmitter set_system_field_interp(int index, double value1, double value2)
        {
            if (Emitter is not null && index >= 0 && index < 4)
            {
                Emitter.mSystemFieldInterp[index][0] = (float)value1;
                Emitter.mSystemFieldInterp[index][1] = (float)value2;
            }

            return this;
        }

        public DynValue get_color_override()
        {
            return Emitter is null ? DynValue.Nil : LuaApiUtility.ColorTuple(Emitter.mColorOverride);
        }

        public ShowcaseParticleEmitter set_color_override(DynValue red, DynValue green, DynValue blue, DynValue alpha)
        {
            if (Emitter is not null)
            {
                Emitter.mColorOverride = LuaApiUtility.MergeColor(Emitter.mColorOverride, red, green, blue, alpha);
            }

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

        public ShowcaseParticleEmitter system_move(double x, double y)
        {
            return set_position(x, y);
        }

        public ShowcaseParticleEmitter move(double x, double y)
        {
            return set_position(x, y);
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

        public ShowcaseParticleEmitter clear_image_override()
        {
            if (Emitter is not null)
            {
                Emitter.mImageOverride = null;
            }

            return this;
        }

        public bool has_image_override()
        {
            return Emitter?.mImageOverride is not null;
        }

        public ShowcaseParticleEmitter spawn()
        {
            return spawn(1);
        }

        public ShowcaseParticleEmitter spawn(double count)
        {
            if (Emitter is null || Emitter.mDead)
            {
                return this;
            }

            int spawnCount = Math.Clamp((int)Math.Round(count), 0, 512);
            for (int i = 0; i < spawnCount; i++)
            {
                Emitter.SpawnParticle(i, spawnCount);
            }

            return this;
        }

        public ShowcaseParticleEmitter update_spawning()
        {
            Emitter?.UpdateSpawning();
            return this;
        }

        public bool update_particle(ShowcaseParticleInstance particle)
        {
            return Emitter is not null && particle?.Particle is not null && Emitter.UpdateParticle(particle.Particle);
        }

        public ShowcaseParticleInstance spawn_particle(int index, int spawnCount)
        {
            TodParticle particle = Emitter?.SpawnParticle(index, spawnCount);
            return particle is null ? null : new ShowcaseParticleInstance(particle);
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

        public ShowcaseParticleEmitter cross_fade_emitter(ShowcaseParticleEmitter toEmitter)
        {
            return cross_fade_to(toEmitter);
        }

        public ShowcaseParticleRenderParams get_render_params(ShowcaseParticleInstance particle)
        {
            if (particle?.Particle is null)
            {
                return null;
            }

            ParticleRenderParams renderParams = default;
            return TodParticleEmitter.GetRenderParams(particle.Particle, ref renderParams)
                ? new ShowcaseParticleRenderParams(renderParams)
                : null;
        }

        public ShowcaseParticleEmitter draw_particle(LuaGraphicsApi graphics, ShowcaseParticleInstance particle)
        {
            if (graphics is not null && Emitter is not null && particle?.Particle is not null)
            {
                Emitter.DrawParticle(graphics.Graphics, particle.Particle);
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

        public int particle_index(ShowcaseParticleInstance particle)
        {
            if (Emitter is null || particle?.Particle is null)
            {
                return -1;
            }

            int index = 0;
            for (LinkedListNode<ParticleID> node = Emitter.mParticleList.First; node is not null; node = node.Next)
            {
                TodParticle current = Emitter.mParticleSystem.mParticleHolder.mParticles.DataArrayTryToGet(node.Value);
                if (ReferenceEquals(current, particle.Particle))
                {
                    return index;
                }

                index++;
            }

            return -1;
        }

        public ShowcaseParticleEmitter delete_particle(ShowcaseParticleInstance particle)
        {
            if (Emitter is not null &&
                particle?.Particle is not null &&
                ReferenceEquals(particle.Particle.mParticleEmitter, Emitter))
            {
                Emitter.DeleteParticle(particle.Particle);
            }

            return this;
        }

        private static int ClampColor(double value)
        {
            return Math.Clamp((int)Math.Round(value), 0, 255);
        }

        private static Image RequireImage(string imageId)
        {
            Image image = ResourceHandler.GetImage(imageId);
            return image ?? throw new InvalidOperationException($"Image '{imageId}' was not found in the current project.");
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
