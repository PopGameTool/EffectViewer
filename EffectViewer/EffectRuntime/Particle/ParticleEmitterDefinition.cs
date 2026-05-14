namespace EffectViewer.EffectRuntime.Particle
{
    public class ParticleEmitterDefinition
    {
        public string mImage;
        public int mImageCol;
        public int mImageRow;
        public int mImageFrames;
        public int mAnimated;
        public int mParticleFlags;
        public EmitterType mEmitterType;
        public string mName;
        public string mOnDuration;
        public readonly FloatParameterTrack mSystemDuration = new();
        public readonly FloatParameterTrack mCrossFadeDuration = new();
        public readonly FloatParameterTrack mSpawnRate = new();
        public readonly FloatParameterTrack mSpawnMinActive = new();
        public readonly FloatParameterTrack mSpawnMaxActive = new();
        public readonly FloatParameterTrack mSpawnMaxLaunched = new();
        public readonly FloatParameterTrack mEmitterRadius = new();
        public readonly FloatParameterTrack mEmitterOffsetX = new();
        public readonly FloatParameterTrack mEmitterOffsetY = new();
        public readonly FloatParameterTrack mEmitterBoxX = new();
        public readonly FloatParameterTrack mEmitterBoxY = new();
        public readonly FloatParameterTrack mEmitterSkewX = new();
        public readonly FloatParameterTrack mEmitterSkewY = new();
        public readonly FloatParameterTrack mEmitterPath = new();
        public readonly FloatParameterTrack mParticleDuration = new();
        public readonly FloatParameterTrack mLaunchSpeed = new();
        public readonly FloatParameterTrack mLaunchAngle = new();
        public readonly FloatParameterTrack mSystemRed = new();
        public readonly FloatParameterTrack mSystemGreen = new();
        public readonly FloatParameterTrack mSystemBlue = new();
        public readonly FloatParameterTrack mSystemAlpha = new();
        public readonly FloatParameterTrack mSystemBrightness = new();
        public ParticleField[] mParticleFields;
        public int mParticleFieldCount;
        public ParticleField[] mSystemFields;
        public int mSystemFieldCount;
        public readonly FloatParameterTrack mParticleRed = new();
        public readonly FloatParameterTrack mParticleGreen = new();
        public readonly FloatParameterTrack mParticleBlue = new();
        public readonly FloatParameterTrack mParticleAlpha = new();
        public readonly FloatParameterTrack mParticleBrightness = new();
        public readonly FloatParameterTrack mParticleSpinAngle = new();
        public readonly FloatParameterTrack mParticleSpinSpeed = new();
        public readonly FloatParameterTrack mParticleScale = new();
        public readonly FloatParameterTrack mParticleStretch = new();
        public readonly FloatParameterTrack mCollisionReflect = new();
        public readonly FloatParameterTrack mCollisionSpin = new();
        public readonly FloatParameterTrack mClipTop = new();
        public readonly FloatParameterTrack mClipBottom = new();
        public readonly FloatParameterTrack mClipLeft = new();
        public readonly FloatParameterTrack mClipRight = new();
        public readonly FloatParameterTrack mAnimationRate = new();

        public ParticleEmitterDefinition()
        {
            mImageRow = 0;
            mImageCol = 0;
            mImageFrames = 1;
            mAnimated = 0;
            mEmitterType = EmitterType.Box;
            mImage = null;
            mName = "";
            mOnDuration = "";
            mParticleFlags = 0;
        }

        public void Dispose()
        {
        }
    }
}