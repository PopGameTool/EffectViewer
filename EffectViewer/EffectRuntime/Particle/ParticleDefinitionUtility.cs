using System;

namespace EffectViewer.EffectRuntime.Particle
{
    internal static class ParticleDefinitionUtility
    {
        public static ParticleDefinition CreateEmpty()
        {
            return new ParticleDefinition
            {
                mEmitterDefs = [],
                mEmitterDefCount = 0
            };
        }

        public static ParticleDefinition Clone(ParticleDefinition source)
        {
            if (source is null)
            {
                return CreateEmpty();
            }

            int count = SafeCount(source.mEmitterDefs, source.mEmitterDefCount);
            ParticleDefinition clone = new()
            {
                mEmitterDefs = new ParticleEmitterDefinition[count],
                mEmitterDefCount = count
            };

            for (int i = 0; i < count; i++)
            {
                clone.mEmitterDefs[i] = CloneEmitter(source.mEmitterDefs[i]);
            }

            return clone;
        }

        public static void ApplyRuntimeDefaults(ParticleDefinition definition)
        {
            if (definition?.mEmitterDefs is null)
            {
                return;
            }

            int count = SafeCount(definition.mEmitterDefs, definition.mEmitterDefCount);
            definition.mEmitterDefCount = count;
            for (int i = 0; i < count; i++)
            {
                ApplyRuntimeDefaults(definition.mEmitterDefs[i]);
            }
        }

        private static ParticleEmitterDefinition CloneEmitter(ParticleEmitterDefinition source)
        {
            if (source is null)
            {
                return new ParticleEmitterDefinition();
            }

            ParticleEmitterDefinition clone = new()
            {
                mImage = source.mImage,
                mImageCol = source.mImageCol,
                mImageRow = source.mImageRow,
                mImageFrames = source.mImageFrames,
                mAnimated = source.mAnimated,
                mParticleFlags = source.mParticleFlags,
                mEmitterType = source.mEmitterType,
                mName = source.mName,
                mOnDuration = source.mOnDuration,
                mParticleFields = CloneFields(source.mParticleFields, source.mParticleFieldCount),
                mSystemFields = CloneFields(source.mSystemFields, source.mSystemFieldCount)
            };

            clone.mParticleFieldCount = clone.mParticleFields?.Length ?? 0;
            clone.mSystemFieldCount = clone.mSystemFields?.Length ?? 0;

            CopyTrack(source.mSystemDuration, clone.mSystemDuration);
            CopyTrack(source.mCrossFadeDuration, clone.mCrossFadeDuration);
            CopyTrack(source.mSpawnRate, clone.mSpawnRate);
            CopyTrack(source.mSpawnMinActive, clone.mSpawnMinActive);
            CopyTrack(source.mSpawnMaxActive, clone.mSpawnMaxActive);
            CopyTrack(source.mSpawnMaxLaunched, clone.mSpawnMaxLaunched);
            CopyTrack(source.mEmitterRadius, clone.mEmitterRadius);
            CopyTrack(source.mEmitterOffsetX, clone.mEmitterOffsetX);
            CopyTrack(source.mEmitterOffsetY, clone.mEmitterOffsetY);
            CopyTrack(source.mEmitterBoxX, clone.mEmitterBoxX);
            CopyTrack(source.mEmitterBoxY, clone.mEmitterBoxY);
            CopyTrack(source.mEmitterSkewX, clone.mEmitterSkewX);
            CopyTrack(source.mEmitterSkewY, clone.mEmitterSkewY);
            CopyTrack(source.mEmitterPath, clone.mEmitterPath);
            CopyTrack(source.mParticleDuration, clone.mParticleDuration);
            CopyTrack(source.mLaunchSpeed, clone.mLaunchSpeed);
            CopyTrack(source.mLaunchAngle, clone.mLaunchAngle);
            CopyTrack(source.mSystemRed, clone.mSystemRed);
            CopyTrack(source.mSystemGreen, clone.mSystemGreen);
            CopyTrack(source.mSystemBlue, clone.mSystemBlue);
            CopyTrack(source.mSystemAlpha, clone.mSystemAlpha);
            CopyTrack(source.mSystemBrightness, clone.mSystemBrightness);
            CopyTrack(source.mParticleRed, clone.mParticleRed);
            CopyTrack(source.mParticleGreen, clone.mParticleGreen);
            CopyTrack(source.mParticleBlue, clone.mParticleBlue);
            CopyTrack(source.mParticleAlpha, clone.mParticleAlpha);
            CopyTrack(source.mParticleBrightness, clone.mParticleBrightness);
            CopyTrack(source.mParticleSpinAngle, clone.mParticleSpinAngle);
            CopyTrack(source.mParticleSpinSpeed, clone.mParticleSpinSpeed);
            CopyTrack(source.mParticleScale, clone.mParticleScale);
            CopyTrack(source.mParticleStretch, clone.mParticleStretch);
            CopyTrack(source.mCollisionReflect, clone.mCollisionReflect);
            CopyTrack(source.mCollisionSpin, clone.mCollisionSpin);
            CopyTrack(source.mClipTop, clone.mClipTop);
            CopyTrack(source.mClipBottom, clone.mClipBottom);
            CopyTrack(source.mClipLeft, clone.mClipLeft);
            CopyTrack(source.mClipRight, clone.mClipRight);
            CopyTrack(source.mAnimationRate, clone.mAnimationRate);

            return clone;
        }

        private static ParticleField[] CloneFields(ParticleField[] source, int sourceCount)
        {
            int count = SafeCount(source, sourceCount);
            if (count == 0)
            {
                return null;
            }

            ParticleField[] clone = new ParticleField[count];
            for (int i = 0; i < count; i++)
            {
                ParticleField sourceField = source[i];
                ParticleField cloneField = new()
                {
                    mFieldType = sourceField?.mFieldType ?? ParticleFieldType.Invalid
                };
                if (sourceField is not null)
                {
                    CopyTrack(sourceField.mX, cloneField.mX);
                    CopyTrack(sourceField.mY, cloneField.mY);
                }

                clone[i] = cloneField;
            }

            return clone;
        }

        private static void CopyTrack(FloatParameterTrack source, FloatParameterTrack target)
        {
            if (target is null)
            {
                return;
            }

            if (source?.mNodes is null || source.mCountNodes <= 0)
            {
                target.mNodes = source?.mNodes is null ? null : [];
                target.mCountNodes = 0;
                return;
            }

            int count = SafeCount(source.mNodes, source.mCountNodes);
            target.mNodes = new FloatParameterTrackNode[count];
            target.mCountNodes = count;
            for (int i = 0; i < count; i++)
            {
                FloatParameterTrackNode node = source.mNodes[i];
                target.mNodes[i] = new FloatParameterTrackNode
                {
                    mTime = node.mTime,
                    mLowValue = node.mLowValue,
                    mHighValue = node.mHighValue,
                    mCurveType = node.mCurveType,
                    mDistribution = node.mDistribution
                };
            }
        }

        private static void ApplyRuntimeDefaults(ParticleEmitterDefinition emitter)
        {
            if (emitter is null)
            {
                return;
            }

            Definition.FloatTrackSetDefault(emitter.mSystemDuration, 0f);
            Definition.FloatTrackSetDefault(emitter.mSpawnRate, 0f);
            Definition.FloatTrackSetDefault(emitter.mSpawnMinActive, -1f);
            Definition.FloatTrackSetDefault(emitter.mSpawnMaxActive, -1f);
            Definition.FloatTrackSetDefault(emitter.mSpawnMaxLaunched, -1f);
            Definition.FloatTrackSetDefault(emitter.mEmitterRadius, 0f);
            Definition.FloatTrackSetDefault(emitter.mEmitterOffsetX, 0f);
            Definition.FloatTrackSetDefault(emitter.mEmitterOffsetY, 0f);
            Definition.FloatTrackSetDefault(emitter.mEmitterBoxX, 0f);
            Definition.FloatTrackSetDefault(emitter.mEmitterBoxY, 0f);
            Definition.FloatTrackSetDefault(emitter.mEmitterSkewX, 0f);
            Definition.FloatTrackSetDefault(emitter.mEmitterSkewY, 0f);
            Definition.FloatTrackSetDefault(emitter.mEmitterPath, 0f);
            Definition.FloatTrackSetDefault(emitter.mParticleDuration, 100f);
            Definition.FloatTrackSetDefault(emitter.mLaunchSpeed, 0f);
            Definition.FloatTrackSetDefault(emitter.mSystemRed, 1f);
            Definition.FloatTrackSetDefault(emitter.mSystemGreen, 1f);
            Definition.FloatTrackSetDefault(emitter.mSystemBlue, 1f);
            Definition.FloatTrackSetDefault(emitter.mSystemAlpha, 1f);
            Definition.FloatTrackSetDefault(emitter.mSystemBrightness, 1f);
            Definition.FloatTrackSetDefault(emitter.mLaunchAngle, 0f);
            Definition.FloatTrackSetDefault(emitter.mCrossFadeDuration, 0f);
            Definition.FloatTrackSetDefault(emitter.mParticleRed, 1f);
            Definition.FloatTrackSetDefault(emitter.mParticleGreen, 1f);
            Definition.FloatTrackSetDefault(emitter.mParticleBlue, 1f);
            Definition.FloatTrackSetDefault(emitter.mParticleAlpha, 1f);
            Definition.FloatTrackSetDefault(emitter.mParticleBrightness, 1f);
            Definition.FloatTrackSetDefault(emitter.mParticleSpinAngle, 0f);
            Definition.FloatTrackSetDefault(emitter.mParticleSpinSpeed, 0f);
            Definition.FloatTrackSetDefault(emitter.mParticleScale, 1f);
            Definition.FloatTrackSetDefault(emitter.mParticleStretch, 1f);
            Definition.FloatTrackSetDefault(emitter.mCollisionReflect, 0f);
            Definition.FloatTrackSetDefault(emitter.mCollisionSpin, 0f);
            Definition.FloatTrackSetDefault(emitter.mClipTop, 0f);
            Definition.FloatTrackSetDefault(emitter.mClipBottom, 0f);
            Definition.FloatTrackSetDefault(emitter.mClipLeft, 0f);
            Definition.FloatTrackSetDefault(emitter.mClipRight, 0f);
            Definition.FloatTrackSetDefault(emitter.mAnimationRate, 0f);
        }

        private static int SafeCount<T>(T[] values, int count)
        {
            return values is null ? 0 : Math.Min(values.Length, count);
        }
    }
}
