using System;
using System.Runtime.CompilerServices;

namespace EffectViewer.EffectRuntime.Particle
{
    public class ParticleInstance : IDataArrayItem
    {
        public ParticleEmitter mParticleEmitter;
        public int mParticleDuration;
        public int mParticleAge;
        public float mParticleTimeValue;
        public float mParticleLastTimeValue;
        public float mAnimationTimeValue;
        public Vector2 mVelocity;
        public Vector2 mPosition;
        public int mImageFrame;
        public float mSpinPosition;
        public float mSpinVelocity;
        public ParticleID mCrossFadeParticleID;
        public int mCrossFadeDuration;
        public InlineArray16<float> mParticleInterp; // ParticleTracks.NumParticleTracks
        public InlineArray4<InlineArray2<float>> mParticleFieldInterp; // EffectConstants.MAX_PARTICLE_FIELDS

        uint IDataArrayItem.Id { get; set; }
        int IDataArrayItem.Index { get; init; }

        public ParticleInstance()
        {
            Reset();
        }

        public void Reset()
        {
            mParticleEmitter = null;
            mParticleDuration = 0;
            mParticleAge = 0;
            mParticleTimeValue = 0f;
            mParticleLastTimeValue = 0f;
            mAnimationTimeValue = 0f;
            mVelocity = default;
            mPosition = default;
            mImageFrame = 0;
            mSpinPosition = 0f;
            mSpinVelocity = 0f;
            mCrossFadeParticleID = ParticleID.Null;
            mCrossFadeDuration = 0;
            foreach (ref float item in mParticleInterp)
            {
                item = 0f;
            }
            foreach (ref InlineArray2<float> item in mParticleFieldInterp)
            {
                item[0] = 0f;
                item[1] = 0f;
            }
        }

        public void Dispose()
        {

        }
    }
}