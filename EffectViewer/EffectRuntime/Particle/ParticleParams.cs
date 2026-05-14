namespace EffectViewer.EffectRuntime.Particle
{
    public class ParticleParams
    {
        public string mParticleEffect;
        public string mParticleFileName;

        public ParticleParams(string aParticleEffect, string aParticleName)
        {
            mParticleEffect = aParticleEffect;
            mParticleFileName = aParticleName;
        }
    }
}