namespace EffectViewer.EffectRuntime.Particle
{
    public class ParticleHolder
    {
        public readonly EffectSystem mEffectSystem;
        public readonly DataArray<ParticleEmitter, ParticleEmitterID> mEmitters = new();
        public readonly DataArray<ParticleInstance, ParticleID> mParticles = new();
        public readonly DataArray<ParticleSystem, ParticleSystemID> mParticleSystems = new();

        public ParticleHolder(EffectSystem effectSystem = null)
        {
            mEffectSystem = effectSystem;
        }

        public void Dispose()
        {
            DisposeHolder();
        }

        public void InitializeHolder()
        {
            mParticleSystems.DataArrayInitialize(1024U, "particle systems");
            mEmitters.DataArrayInitialize(1024U, "emitters");
            mParticles.DataArrayInitialize(1024U, "particles");
        }

        public void DisposeHolder()
        {
            mParticleSystems.DataArrayDispose();
            mEmitters.DataArrayDispose();
            mParticles.DataArrayDispose();
        }

        public ParticleSystem AllocParticleSystemFromDef(float theX, float theY, int theRenderOrder, ParticleDefinition theDefinition, string theParticleEffect)
        {
            if (mParticleSystems.mSize == mParticleSystems.mMaxSize)
            {
                Debug.Log(DebugType.Warn, "Too many particle systems");
                return null;
            }

            if (theDefinition.mEmitterDefCount + mEmitters.mSize > mEmitters.mMaxSize)
            {
                Debug.Log(DebugType.Warn, "Too many particle emitters");
                return null;
            }

            ParticleSystem particleSystem = mParticleSystems.DataArrayAlloc();
            particleSystem.mParticleHolder = this;
            particleSystem.InitializeFromDefinition(theX, theY, theRenderOrder, theDefinition, theParticleEffect);
            return particleSystem;
        }

        public ParticleSystem AllocParticleSystem(float theX, float theY, int theRenderOrder, string theParticleEffect)
        {
            Debug.Assert(ParticleUtility.gParticleDefArray.ContainsKey(theParticleEffect));
            ParticleDefinition aDefinition = ParticleUtility.gParticleDefArray[theParticleEffect];
            return AllocParticleSystemFromDef(theX, theY, theRenderOrder, aDefinition, theParticleEffect);
        }

        public bool IsOverLoaded()
        {
            return mParticleSystems.mSize > 900 || mEmitters.mSize > 900 || mParticles.mSize > 900;
        }
    }
}
