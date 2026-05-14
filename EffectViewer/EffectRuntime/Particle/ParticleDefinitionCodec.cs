using System;
using System.Collections.Generic;
using System.IO;

namespace EffectViewer.EffectRuntime.Particle
{
    public static class ParticleDefinitionCodec
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

        private static readonly DefSymbol[] CompiledParticleFlagSymbols =
        [
            new(0, "RandomLaunchSpin"),
            new(1, "AlignLaunchSpin"),
            new(2, "AlignToPixel"),
            new(4, "ParticleLoops"),
            new(3, "SystemLoops"),
            new(5, "ParticlesDontFollow"),
            new(6, "RandomStartTime"),
            new(7, "DieIfOverloaded"),
            new(8, "Additive"),
            new(9, "FullScreen"),
            new(10, "SoftwareOnly"),
            new(11, "HardwareOnly")
        ];

        private static readonly IReadOnlyDictionary<string, EmitterType> EmitterTypeSymbols =
            new Dictionary<string, EmitterType>(StringComparer.OrdinalIgnoreCase)
            {
                ["Circle"] = EmitterType.Circle,
                ["Box"] = EmitterType.Box,
                ["BoxPath"] = EmitterType.BoxPath,
                ["CirclePath"] = EmitterType.CirclePath,
                ["CircleEvenSpacing"] = EmitterType.CircleEvenSpacing
            };

        private static readonly DefSymbol[] CompiledEmitterTypeSymbols =
        [
            new(0, "Circle"),
            new(1, "Box"),
            new(2, "BoxPath"),
            new(3, "CirclePath"),
            new(4, "CircleEvenSpacing")
        ];

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

        private static readonly DefSymbol[] CompiledParticleFieldTypeSymbols =
        [
            new(1, "Friction"),
            new(2, "Acceleration"),
            new(3, "Attractor"),
            new(4, "MaxVelocity"),
            new(5, "Velocity"),
            new(6, "Position"),
            new(7, "SystemPosition"),
            new(8, "GroundConstraint"),
            new(9, "Shake"),
            new(10, "Circle"),
            new(11, "Away")
        ];

        private static readonly DefMap<ParticleField> ParticleFieldDefMap = new(
            0x14,
            () => new ParticleField(),
            DefinitionMapLoader.Enum<ParticleField, ParticleFieldType>("FieldType", ParticleFieldTypeSymbols, static (ref ParticleField field, ParticleFieldType value) => field.mFieldType = value, static (ref ParticleField field) => field.mFieldType, compiledOffset: 0x0, compiledSymbols: CompiledParticleFieldTypeSymbols),
            DefinitionMapLoader.TrackFloat<ParticleField>("x", static (ref ParticleField field) => field.mX, compiledOffset: 0x4),
            DefinitionMapLoader.TrackFloat<ParticleField>("y", static (ref ParticleField field) => field.mY, compiledOffset: 0xC));

        private static readonly DefMap<ParticleEmitterDefinition> EmitterDefMap = new(
            0x164,
            () => new ParticleEmitterDefinition(),
            DefinitionMapLoader.Image<ParticleEmitterDefinition>("Image", static (ref ParticleEmitterDefinition emitter, string value) => emitter.mImage = value, static (ref ParticleEmitterDefinition emitter) => emitter.mImage, compiledOffset: 0x0),
            DefinitionMapLoader.Int<ParticleEmitterDefinition>("ImageRow", static (ref ParticleEmitterDefinition emitter, int value) => emitter.mImageRow = value, static (ref ParticleEmitterDefinition emitter) => emitter.mImageRow, compiledOffset: 0x8),
            DefinitionMapLoader.Int<ParticleEmitterDefinition>("ImageCol", static (ref ParticleEmitterDefinition emitter, int value) => emitter.mImageCol = value, static (ref ParticleEmitterDefinition emitter) => emitter.mImageCol, compiledOffset: 0x4),
            DefinitionMapLoader.Int<ParticleEmitterDefinition>("ImageFrames", static (ref ParticleEmitterDefinition emitter, int value) => emitter.mImageFrames = value, static (ref ParticleEmitterDefinition emitter) => emitter.mImageFrames, static value => value != 1, compiledOffset: 0xC),
            DefinitionMapLoader.Int<ParticleEmitterDefinition>("Animated", static (ref ParticleEmitterDefinition emitter, int value) => emitter.mAnimated = value, static (ref ParticleEmitterDefinition emitter) => emitter.mAnimated, compiledOffset: 0x10),
            DefinitionMapLoader.Flags<ParticleEmitterDefinition>("ParticleFlags", ParticleFlagSymbols, static (ref ParticleEmitterDefinition emitter, int bitIndex, bool value) => SetBit(ref emitter.mParticleFlags, bitIndex, value), static (ref ParticleEmitterDefinition emitter) => emitter.mParticleFlags, compiledOffset: 0x14, compiledSymbols: CompiledParticleFlagSymbols),
            DefinitionMapLoader.Enum<ParticleEmitterDefinition, EmitterType>("EmitterType", EmitterTypeSymbols, static (ref ParticleEmitterDefinition emitter, EmitterType value) => emitter.mEmitterType = value, static (ref ParticleEmitterDefinition emitter) => emitter.mEmitterType, static value => value != EmitterType.Box, compiledOffset: 0x18, compiledSymbols: CompiledEmitterTypeSymbols),
            DefinitionMapLoader.String<ParticleEmitterDefinition>("Name", static (ref ParticleEmitterDefinition emitter, string value) => emitter.mName = value, static (ref ParticleEmitterDefinition emitter) => emitter.mName, compiledOffset: 0x1C),
            DefinitionMapLoader.TrackFloat<ParticleEmitterDefinition>("SystemDuration", static (ref ParticleEmitterDefinition emitter) => emitter.mSystemDuration, compiledOffset: 0x24),
            DefinitionMapLoader.String<ParticleEmitterDefinition>("OnDuration", static (ref ParticleEmitterDefinition emitter, string value) => emitter.mOnDuration = value, static (ref ParticleEmitterDefinition emitter) => emitter.mOnDuration, compiledOffset: 0x20),
            DefinitionMapLoader.TrackFloat<ParticleEmitterDefinition>("CrossFadeDuration", static (ref ParticleEmitterDefinition emitter) => emitter.mCrossFadeDuration, compiledOffset: 0x2C),
            DefinitionMapLoader.TrackFloat<ParticleEmitterDefinition>("SpawnRate", static (ref ParticleEmitterDefinition emitter) => emitter.mSpawnRate, compiledOffset: 0x34),
            DefinitionMapLoader.TrackFloat<ParticleEmitterDefinition>("SpawnMinActive", static (ref ParticleEmitterDefinition emitter) => emitter.mSpawnMinActive, compiledOffset: 0x3C),
            DefinitionMapLoader.TrackFloat<ParticleEmitterDefinition>("SpawnMaxActive", static (ref ParticleEmitterDefinition emitter) => emitter.mSpawnMaxActive, compiledOffset: 0x44),
            DefinitionMapLoader.TrackFloat<ParticleEmitterDefinition>("SpawnMaxLaunched", static (ref ParticleEmitterDefinition emitter) => emitter.mSpawnMaxLaunched, compiledOffset: 0x4C),
            DefinitionMapLoader.TrackFloat<ParticleEmitterDefinition>("EmitterRadius", static (ref ParticleEmitterDefinition emitter) => emitter.mEmitterRadius, compiledOffset: 0x54),
            DefinitionMapLoader.TrackFloat<ParticleEmitterDefinition>("EmitterOffsetX", static (ref ParticleEmitterDefinition emitter) => emitter.mEmitterOffsetX, compiledOffset: 0x5C),
            DefinitionMapLoader.TrackFloat<ParticleEmitterDefinition>("EmitterOffsetY", static (ref ParticleEmitterDefinition emitter) => emitter.mEmitterOffsetY, compiledOffset: 0x64),
            DefinitionMapLoader.TrackFloat<ParticleEmitterDefinition>("EmitterBoxX", static (ref ParticleEmitterDefinition emitter) => emitter.mEmitterBoxX, compiledOffset: 0x6C),
            DefinitionMapLoader.TrackFloat<ParticleEmitterDefinition>("EmitterBoxY", static (ref ParticleEmitterDefinition emitter) => emitter.mEmitterBoxY, compiledOffset: 0x74),
            DefinitionMapLoader.TrackFloat<ParticleEmitterDefinition>("EmitterPath", static (ref ParticleEmitterDefinition emitter) => emitter.mEmitterPath, compiledOffset: 0x8C),
            DefinitionMapLoader.TrackFloat<ParticleEmitterDefinition>("EmitterSkewX", static (ref ParticleEmitterDefinition emitter) => emitter.mEmitterSkewX, compiledOffset: 0x7C),
            DefinitionMapLoader.TrackFloat<ParticleEmitterDefinition>("EmitterSkewY", static (ref ParticleEmitterDefinition emitter) => emitter.mEmitterSkewY, compiledOffset: 0x84),
            DefinitionMapLoader.TrackFloat<ParticleEmitterDefinition>("ParticleDuration", static (ref ParticleEmitterDefinition emitter) => emitter.mParticleDuration, compiledOffset: 0x94),
            DefinitionMapLoader.TrackFloat<ParticleEmitterDefinition>("SystemRed", static (ref ParticleEmitterDefinition emitter) => emitter.mSystemRed, compiledOffset: 0xAC),
            DefinitionMapLoader.TrackFloat<ParticleEmitterDefinition>("SystemGreen", static (ref ParticleEmitterDefinition emitter) => emitter.mSystemGreen, compiledOffset: 0xB4),
            DefinitionMapLoader.TrackFloat<ParticleEmitterDefinition>("SystemBlue", static (ref ParticleEmitterDefinition emitter) => emitter.mSystemBlue, compiledOffset: 0xBC),
            DefinitionMapLoader.TrackFloat<ParticleEmitterDefinition>("SystemAlpha", static (ref ParticleEmitterDefinition emitter) => emitter.mSystemAlpha, compiledOffset: 0xC4),
            DefinitionMapLoader.TrackFloat<ParticleEmitterDefinition>("SystemBrightness", static (ref ParticleEmitterDefinition emitter) => emitter.mSystemBrightness, compiledOffset: 0xCC),
            DefinitionMapLoader.TrackFloat<ParticleEmitterDefinition>("LaunchSpeed", static (ref ParticleEmitterDefinition emitter) => emitter.mLaunchSpeed, compiledOffset: 0x9C),
            DefinitionMapLoader.TrackFloat<ParticleEmitterDefinition>("LaunchAngle", static (ref ParticleEmitterDefinition emitter) => emitter.mLaunchAngle, compiledOffset: 0xA4),
            DefinitionMapLoader.Array<ParticleEmitterDefinition, ParticleField>("Field", ParticleFieldDefMap, static (ref ParticleEmitterDefinition emitter, ParticleField field) => AddParticleField(ref emitter, field), static (ref ParticleEmitterDefinition emitter) => SafeCount(emitter.mParticleFields, emitter.mParticleFieldCount), static (ref ParticleEmitterDefinition emitter, int index) => emitter.mParticleFields[index], compiledOffset: 0xD4),
            DefinitionMapLoader.Array<ParticleEmitterDefinition, ParticleField>("SystemField", ParticleFieldDefMap, static (ref ParticleEmitterDefinition emitter, ParticleField field) => AddSystemField(ref emitter, field), static (ref ParticleEmitterDefinition emitter) => SafeCount(emitter.mSystemFields, emitter.mSystemFieldCount), static (ref ParticleEmitterDefinition emitter, int index) => emitter.mSystemFields[index], compiledOffset: 0xDC),
            DefinitionMapLoader.TrackFloat<ParticleEmitterDefinition>("ParticleRed", static (ref ParticleEmitterDefinition emitter) => emitter.mParticleRed, compiledOffset: 0xE4),
            DefinitionMapLoader.TrackFloat<ParticleEmitterDefinition>("ParticleGreen", static (ref ParticleEmitterDefinition emitter) => emitter.mParticleGreen, compiledOffset: 0xEC),
            DefinitionMapLoader.TrackFloat<ParticleEmitterDefinition>("ParticleBlue", static (ref ParticleEmitterDefinition emitter) => emitter.mParticleBlue, compiledOffset: 0xF4),
            DefinitionMapLoader.TrackFloat<ParticleEmitterDefinition>("ParticleAlpha", static (ref ParticleEmitterDefinition emitter) => emitter.mParticleAlpha, compiledOffset: 0xFC),
            DefinitionMapLoader.TrackFloat<ParticleEmitterDefinition>("ParticleBrightness", static (ref ParticleEmitterDefinition emitter) => emitter.mParticleBrightness, compiledOffset: 0x104),
            DefinitionMapLoader.TrackFloat<ParticleEmitterDefinition>("ParticleSpinAngle", static (ref ParticleEmitterDefinition emitter) => emitter.mParticleSpinAngle, compiledOffset: 0x10C),
            DefinitionMapLoader.TrackFloat<ParticleEmitterDefinition>("ParticleSpinSpeed", static (ref ParticleEmitterDefinition emitter) => emitter.mParticleSpinSpeed, compiledOffset: 0x114),
            DefinitionMapLoader.TrackFloat<ParticleEmitterDefinition>("ParticleScale", static (ref ParticleEmitterDefinition emitter) => emitter.mParticleScale, compiledOffset: 0x11C),
            DefinitionMapLoader.TrackFloat<ParticleEmitterDefinition>("ParticleStretch", static (ref ParticleEmitterDefinition emitter) => emitter.mParticleStretch, compiledOffset: 0x124),
            DefinitionMapLoader.TrackFloat<ParticleEmitterDefinition>("CollisionReflect", static (ref ParticleEmitterDefinition emitter) => emitter.mCollisionReflect, compiledOffset: 0x12C),
            DefinitionMapLoader.TrackFloat<ParticleEmitterDefinition>("CollisionSpin", static (ref ParticleEmitterDefinition emitter) => emitter.mCollisionSpin, compiledOffset: 0x134),
            DefinitionMapLoader.TrackFloat<ParticleEmitterDefinition>("ClipTop", static (ref ParticleEmitterDefinition emitter) => emitter.mClipTop, compiledOffset: 0x13C),
            DefinitionMapLoader.TrackFloat<ParticleEmitterDefinition>("ClipBottom", static (ref ParticleEmitterDefinition emitter) => emitter.mClipBottom, compiledOffset: 0x144),
            DefinitionMapLoader.TrackFloat<ParticleEmitterDefinition>("ClipLeft", static (ref ParticleEmitterDefinition emitter) => emitter.mClipLeft, compiledOffset: 0x14C),
            DefinitionMapLoader.TrackFloat<ParticleEmitterDefinition>("ClipRight", static (ref ParticleEmitterDefinition emitter) => emitter.mClipRight, compiledOffset: 0x154),
            DefinitionMapLoader.TrackFloat<ParticleEmitterDefinition>("AnimationRate", static (ref ParticleEmitterDefinition emitter) => emitter.mAnimationRate, compiledOffset: 0x15C));

        private static readonly DefMap<ParticleDefinition> ParticleDefMap = new(
            0x8,
            () => new ParticleDefinition(),
            DefinitionMapLoader.Array<ParticleDefinition, ParticleEmitterDefinition>("Emitter", EmitterDefMap, static (ref ParticleDefinition particles, ParticleEmitterDefinition emitter) => AddEmitter(ref particles, emitter), static (ref ParticleDefinition particles) => SafeCount(particles.mEmitterDefs, particles.mEmitterDefCount), static (ref ParticleDefinition particles, int index) => particles.mEmitterDefs[index], compiledOffset: 0x0));

        public static ParticleDefinition Decode(Stream stream)
        {
            return DefinitionMapLoader.Load(stream, ParticleDefMap);
        }

        public static void Encode(Stream stream, ParticleDefinition definition)
        {
            DefinitionMapLoader.Save(stream, ParticleDefMap, definition);
        }

        public static void Encode(Stream stream, ParticleDefinition definition, string fileName)
        {
            DefinitionMapLoader.Save(stream, ParticleDefMap, definition, fileName);
        }

        public static void WriteXml(Stream stream, ParticleDefinition definition)
        {
            Encode(stream, definition);
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

        private static void AddEmitter(ref ParticleDefinition particles, ParticleEmitterDefinition emitter)
        {
            ParticleEmitterDefinition[] emitters = particles.mEmitterDefs ?? [];
            Array.Resize(ref emitters, emitters.Length + 1);
            emitters[^1] = emitter;
            particles.mEmitterDefs = emitters;
            particles.mEmitterDefCount = emitters.Length;
        }

        private static void AddParticleField(ref ParticleEmitterDefinition emitter, ParticleField field)
        {
            ParticleField[] fields = emitter.mParticleFields ?? [];
            Array.Resize(ref fields, fields.Length + 1);
            fields[^1] = field;
            emitter.mParticleFields = fields;
            emitter.mParticleFieldCount = fields.Length;
        }

        private static void AddSystemField(ref ParticleEmitterDefinition emitter, ParticleField field)
        {
            ParticleField[] fields = emitter.mSystemFields ?? [];
            Array.Resize(ref fields, fields.Length + 1);
            fields[^1] = field;
            emitter.mSystemFields = fields;
            emitter.mSystemFieldCount = fields.Length;
        }

        private static int SafeCount<T>(T[] values, int count)
        {
            return values is null ? 0 : Math.Min(values.Length, count);
        }
    }
}
