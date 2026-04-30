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
        public bool is_dead() => ParticleSystem is null || ParticleSystem.mDead;

        public ShowcaseParticle set_position(double x, double y)
        {
            ParticleSystem?.SystemMove((float)x, (float)y);
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

        public ShowcaseParticle set_scale(double scale)
        {
            ParticleSystem?.OverrideScale(null, (float)scale);
            return this;
        }

        public ShowcaseParticle die()
        {
            ParticleSystem?.ParticleSystemDie();
            return this;
        }

        private static int ClampColor(double value)
        {
            return System.Math.Clamp((int)System.Math.Round(value), 0, 255);
        }
    }
}
