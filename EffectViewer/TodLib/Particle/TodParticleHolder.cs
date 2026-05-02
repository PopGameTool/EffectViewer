namespace EffectViewer.TodLib.Particle
{
    public class TodParticleHolder
    {
        public readonly EffectSystem mEffectSystem;
        public readonly DataArray<TodParticleEmitter, ParticleEmitterID> mEmitters = new();
        public readonly DataArray<TodParticle, ParticleID> mParticles = new();
        public readonly DataArray<TodParticleSystem, ParticleSystemID> mParticleSystems = new();

        public TodParticleHolder(EffectSystem effectSystem = null)
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

        public TodParticleSystem AllocParticleSystemFromDef(float theX, float theY, int theRenderOrder, TodParticleDefinition theDefinition, string theParticleEffect)
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

            TodParticleSystem aTodParticle = mParticleSystems.DataArrayAlloc();
            aTodParticle.mParticleHolder = this;
            aTodParticle.TodParticleInitializeFromDef(theX, theY, theRenderOrder, theDefinition, theParticleEffect);
            return aTodParticle;
        }

        public TodParticleSystem AllocParticleSystem(float theX, float theY, int theRenderOrder, string theParticleEffect)
        {
            Debug.ASSERT(TodParticleGlobal.gParticleDefArray.ContainsKey(theParticleEffect));
            TodParticleDefinition aDefinition = TodParticleGlobal.gParticleDefArray[theParticleEffect];
            return AllocParticleSystemFromDef(theX, theY, theRenderOrder, aDefinition, theParticleEffect);
        }

        public bool IsOverLoaded()
        {
            return mParticleSystems.mSize > 900 || mEmitters.mSize > 900 || mParticles.mSize > 900;
        }
    }
}
