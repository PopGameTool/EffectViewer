using System;
using System.Collections.Generic;
using System.IO;

namespace EffectViewer.TodLib.Particle
{
    public static class SexyParticleReader
    {
        private static readonly IReadOnlyDictionary<string, int> ParticleFlagSymbols =
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["RandomLaunchSpin"] = 0,
                ["AlignLaunchSpin"] = 1,
                ["AlignToPixel"] = 2,
                ["AlignToPixels"] = 2,
                ["SystemLoops"] = 3,
                ["ParticleLoops"] = 4,
                ["ParticlesDontFollow"] = 5,
                ["RandomStartTime"] = 6,
                ["DieIfOverloaded"] = 7,
                ["Additive"] = 8,
                ["FullScreen"] = 9,
                ["Fullscreen"] = 9,
                ["SoftwareOnly"] = 10,
                ["HardwareOnly"] = 11
            };

        private static readonly IReadOnlyDictionary<string, EmitterType> EmitterTypeSymbols =
            new Dictionary<string, EmitterType>(StringComparer.OrdinalIgnoreCase)
            {
                ["Circle"] = EmitterType.Circle,
                ["Box"] = EmitterType.Box,
                ["BoxPath"] = EmitterType.BoxPath,
                ["CirclePath"] = EmitterType.CirclePath,
                ["CircleEvenSpacing"] = EmitterType.CircleEvenSpacing
            };

        private static readonly IReadOnlyDictionary<string, ParticleFieldType> ParticleFieldTypeSymbols =
            new Dictionary<string, ParticleFieldType>(StringComparer.OrdinalIgnoreCase)
            {
                ["Friction"] = ParticleFieldType.Friction,
                ["Acceleration"] = ParticleFieldType.Acceleration,
                ["Attractor"] = ParticleFieldType.Attractor,
                ["MaxVelocity"] = ParticleFieldType.MaxVelocity,
                ["Velocity"] = ParticleFieldType.Velocity,
                ["Position"] = ParticleFieldType.Position,
                ["SystemPosition"] = ParticleFieldType.SystemPosition,
                ["GroundConstraint"] = ParticleFieldType.GroundConstraint,
                ["Shake"] = ParticleFieldType.Shake,
                ["Circle"] = ParticleFieldType.Circle,
                ["Away"] = ParticleFieldType.Away
            };

        private static readonly DefMap<ParticleField> ParticleFieldDefMap = new(
            () => new ParticleField(),
            DefinitionMapLoader.Enum<ParticleField, ParticleFieldType>("FieldType", ParticleFieldTypeSymbols, static (ref ParticleField field, ParticleFieldType value) => field.mFieldType = value),
            DefinitionMapLoader.TrackFloat<ParticleField>("x", static (ref ParticleField field) => field.mX),
            DefinitionMapLoader.TrackFloat<ParticleField>("y", static (ref ParticleField field) => field.mY));

        private static readonly DefMap<TodEmitterDefinition> EmitterDefMap = new(
            () => new TodEmitterDefinition(),
            DefinitionMapLoader.Image<TodEmitterDefinition>("Image", static (ref TodEmitterDefinition emitter, string value) => emitter.mImage = value),
            DefinitionMapLoader.Int<TodEmitterDefinition>("ImageRow", static (ref TodEmitterDefinition emitter, int value) => emitter.mImageRow = value),
            DefinitionMapLoader.Int<TodEmitterDefinition>("ImageCol", static (ref TodEmitterDefinition emitter, int value) => emitter.mImageCol = value),
            DefinitionMapLoader.Int<TodEmitterDefinition>("ImageFrames", static (ref TodEmitterDefinition emitter, int value) => emitter.mImageFrames = value),
            DefinitionMapLoader.Int<TodEmitterDefinition>("Animated", static (ref TodEmitterDefinition emitter, int value) => emitter.mAnimated = value),
            DefinitionMapLoader.Flags<TodEmitterDefinition>("ParticleFlags", ParticleFlagSymbols, static (ref TodEmitterDefinition emitter, int bitIndex, bool value) => SetBit(ref emitter.mParticleFlags, bitIndex, value)),
            DefinitionMapLoader.Enum<TodEmitterDefinition, EmitterType>("EmitterType", EmitterTypeSymbols, static (ref TodEmitterDefinition emitter, EmitterType value) => emitter.mEmitterType = value),
            DefinitionMapLoader.String<TodEmitterDefinition>("Name", static (ref TodEmitterDefinition emitter, string value) => emitter.mName = value),
            DefinitionMapLoader.TrackFloat<TodEmitterDefinition>("SystemDuration", static (ref TodEmitterDefinition emitter) => emitter.mSystemDuration),
            DefinitionMapLoader.String<TodEmitterDefinition>("OnDuration", static (ref TodEmitterDefinition emitter, string value) => emitter.mOnDuration = value),
            DefinitionMapLoader.TrackFloat<TodEmitterDefinition>("CrossFadeDuration", static (ref TodEmitterDefinition emitter) => emitter.mCrossFadeDuration),
            DefinitionMapLoader.TrackFloat<TodEmitterDefinition>("SpawnRate", static (ref TodEmitterDefinition emitter) => emitter.mSpawnRate),
            DefinitionMapLoader.TrackFloat<TodEmitterDefinition>("SpawnMinActive", static (ref TodEmitterDefinition emitter) => emitter.mSpawnMinActive),
            DefinitionMapLoader.TrackFloat<TodEmitterDefinition>("SpawnMaxActive", static (ref TodEmitterDefinition emitter) => emitter.mSpawnMaxActive),
            DefinitionMapLoader.TrackFloat<TodEmitterDefinition>("SpawnMaxLaunched", static (ref TodEmitterDefinition emitter) => emitter.mSpawnMaxLaunched),
            DefinitionMapLoader.TrackFloat<TodEmitterDefinition>("EmitterRadius", static (ref TodEmitterDefinition emitter) => emitter.mEmitterRadius),
            DefinitionMapLoader.TrackFloat<TodEmitterDefinition>("EmitterOffsetX", static (ref TodEmitterDefinition emitter) => emitter.mEmitterOffsetX),
            DefinitionMapLoader.TrackFloat<TodEmitterDefinition>("EmitterOffsetY", static (ref TodEmitterDefinition emitter) => emitter.mEmitterOffsetY),
            DefinitionMapLoader.TrackFloat<TodEmitterDefinition>("EmitterBoxX", static (ref TodEmitterDefinition emitter) => emitter.mEmitterBoxX),
            DefinitionMapLoader.TrackFloat<TodEmitterDefinition>("EmitterBoxY", static (ref TodEmitterDefinition emitter) => emitter.mEmitterBoxY),
            DefinitionMapLoader.TrackFloat<TodEmitterDefinition>("EmitterSkewX", static (ref TodEmitterDefinition emitter) => emitter.mEmitterSkewX),
            DefinitionMapLoader.TrackFloat<TodEmitterDefinition>("EmitterSkewY", static (ref TodEmitterDefinition emitter) => emitter.mEmitterSkewY),
            DefinitionMapLoader.TrackFloat<TodEmitterDefinition>("EmitterPath", static (ref TodEmitterDefinition emitter) => emitter.mEmitterPath),
            DefinitionMapLoader.TrackFloat<TodEmitterDefinition>("ParticleDuration", static (ref TodEmitterDefinition emitter) => emitter.mParticleDuration),
            DefinitionMapLoader.TrackFloat<TodEmitterDefinition>("SystemRed", static (ref TodEmitterDefinition emitter) => emitter.mSystemRed),
            DefinitionMapLoader.TrackFloat<TodEmitterDefinition>("SystemGreen", static (ref TodEmitterDefinition emitter) => emitter.mSystemGreen),
            DefinitionMapLoader.TrackFloat<TodEmitterDefinition>("SystemBlue", static (ref TodEmitterDefinition emitter) => emitter.mSystemBlue),
            DefinitionMapLoader.TrackFloat<TodEmitterDefinition>("SystemAlpha", static (ref TodEmitterDefinition emitter) => emitter.mSystemAlpha),
            DefinitionMapLoader.TrackFloat<TodEmitterDefinition>("SystemBrightness", static (ref TodEmitterDefinition emitter) => emitter.mSystemBrightness),
            DefinitionMapLoader.TrackFloat<TodEmitterDefinition>("LaunchSpeed", static (ref TodEmitterDefinition emitter) => emitter.mLaunchSpeed),
            DefinitionMapLoader.TrackFloat<TodEmitterDefinition>("LaunchAngle", static (ref TodEmitterDefinition emitter) => emitter.mLaunchAngle),
            DefinitionMapLoader.Array<TodEmitterDefinition, ParticleField>("Field", ParticleFieldDefMap, static (ref TodEmitterDefinition emitter, ParticleField field) => AddParticleField(ref emitter, field)),
            DefinitionMapLoader.Array<TodEmitterDefinition, ParticleField>("SystemField", ParticleFieldDefMap, static (ref TodEmitterDefinition emitter, ParticleField field) => AddSystemField(ref emitter, field)),
            DefinitionMapLoader.TrackFloat<TodEmitterDefinition>("ParticleRed", static (ref TodEmitterDefinition emitter) => emitter.mParticleRed),
            DefinitionMapLoader.TrackFloat<TodEmitterDefinition>("ParticleGreen", static (ref TodEmitterDefinition emitter) => emitter.mParticleGreen),
            DefinitionMapLoader.TrackFloat<TodEmitterDefinition>("ParticleBlue", static (ref TodEmitterDefinition emitter) => emitter.mParticleBlue),
            DefinitionMapLoader.TrackFloat<TodEmitterDefinition>("ParticleAlpha", static (ref TodEmitterDefinition emitter) => emitter.mParticleAlpha),
            DefinitionMapLoader.TrackFloat<TodEmitterDefinition>("ParticleBrightness", static (ref TodEmitterDefinition emitter) => emitter.mParticleBrightness),
            DefinitionMapLoader.TrackFloat<TodEmitterDefinition>("ParticleSpinAngle", static (ref TodEmitterDefinition emitter) => emitter.mParticleSpinAngle),
            DefinitionMapLoader.TrackFloat<TodEmitterDefinition>("ParticleSpinSpeed", static (ref TodEmitterDefinition emitter) => emitter.mParticleSpinSpeed),
            DefinitionMapLoader.TrackFloat<TodEmitterDefinition>("ParticleScale", static (ref TodEmitterDefinition emitter) => emitter.mParticleScale),
            DefinitionMapLoader.TrackFloat<TodEmitterDefinition>("ParticleStretch", static (ref TodEmitterDefinition emitter) => emitter.mParticleStretch),
            DefinitionMapLoader.TrackFloat<TodEmitterDefinition>("CollisionReflect", static (ref TodEmitterDefinition emitter) => emitter.mCollisionReflect),
            DefinitionMapLoader.TrackFloat<TodEmitterDefinition>("CollisionSpin", static (ref TodEmitterDefinition emitter) => emitter.mCollisionSpin),
            DefinitionMapLoader.TrackFloat<TodEmitterDefinition>("ClipTop", static (ref TodEmitterDefinition emitter) => emitter.mClipTop),
            DefinitionMapLoader.TrackFloat<TodEmitterDefinition>("ClipBottom", static (ref TodEmitterDefinition emitter) => emitter.mClipBottom),
            DefinitionMapLoader.TrackFloat<TodEmitterDefinition>("ClipLeft", static (ref TodEmitterDefinition emitter) => emitter.mClipLeft),
            DefinitionMapLoader.TrackFloat<TodEmitterDefinition>("ClipRight", static (ref TodEmitterDefinition emitter) => emitter.mClipRight),
            DefinitionMapLoader.TrackFloat<TodEmitterDefinition>("AnimationRate", static (ref TodEmitterDefinition emitter) => emitter.mAnimationRate));

        private static readonly DefMap<TodParticleDefinition> ParticleDefMap = new(
            () => new TodParticleDefinition(),
            DefinitionMapLoader.Array<TodParticleDefinition, TodEmitterDefinition>("Emitter", EmitterDefMap, static (ref TodParticleDefinition particles, TodEmitterDefinition emitter) => AddEmitter(ref particles, emitter)));

        public static TodParticleDefinition Decode(Stream stream)
        {
            return DefinitionMapLoader.Load(stream, ParticleDefMap);
        }

        public static void SetBit(ref int flags, int index, bool value)
        {
            if (value)
            {
                flags |= 1 << index;
            }
            else
            {
                flags &= ~(1 << index);
            }
        }

        public static void ReadTrackNode(string inText, FloatParameterTrack realans)
        {
            DefinitionMapLoader.ReadFloatTrack(inText, realans);
        }

        private static void AddEmitter(ref TodParticleDefinition particles, TodEmitterDefinition emitter)
        {
            TodEmitterDefinition[] emitters = particles.mEmitterDefs ?? [];
            Array.Resize(ref emitters, emitters.Length + 1);
            emitters[^1] = emitter;
            particles.mEmitterDefs = emitters;
            particles.mEmitterDefCount = emitters.Length;
        }

        private static void AddParticleField(ref TodEmitterDefinition emitter, ParticleField field)
        {
            ParticleField[] fields = emitter.mParticleFields ?? [];
            Array.Resize(ref fields, fields.Length + 1);
            fields[^1] = field;
            emitter.mParticleFields = fields;
            emitter.mParticleFieldCount = fields.Length;
        }

        private static void AddSystemField(ref TodEmitterDefinition emitter, ParticleField field)
        {
            ParticleField[] fields = emitter.mSystemFields ?? [];
            Array.Resize(ref fields, fields.Length + 1);
            fields[^1] = field;
            emitter.mSystemFields = fields;
            emitter.mSystemFieldCount = fields.Length;
        }
    }
}
