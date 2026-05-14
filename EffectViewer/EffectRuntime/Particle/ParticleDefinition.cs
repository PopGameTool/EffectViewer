namespace EffectViewer.EffectRuntime.Particle
{
    public class ParticleDefinition
    {
        public ParticleEmitterDefinition[] mEmitterDefs;
        public int mEmitterDefCount;

        public ParticleDefinition()
        {
            mEmitterDefs = null;
            mEmitterDefCount = 0;
        }
    }
}