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

        private static readonly DefMap<TodEmitterDefinition> EmitterDefMap = new(
            0x164,
            () => new TodEmitterDefinition(),
            DefinitionMapLoader.Image<TodEmitterDefinition>("Image", static (ref TodEmitterDefinition emitter, string value) => emitter.mImage = value, static (ref TodEmitterDefinition emitter) => emitter.mImage, compiledOffset: 0x0),
            DefinitionMapLoader.Int<TodEmitterDefinition>("ImageRow", static (ref TodEmitterDefinition emitter, int value) => emitter.mImageRow = value, static (ref TodEmitterDefinition emitter) => emitter.mImageRow, compiledOffset: 0x8),
            DefinitionMapLoader.Int<TodEmitterDefinition>("ImageCol", static (ref TodEmitterDefinition emitter, int value) => emitter.mImageCol = value, static (ref TodEmitterDefinition emitter) => emitter.mImageCol, compiledOffset: 0x4),
            DefinitionMapLoader.Int<TodEmitterDefinition>("ImageFrames", static (ref TodEmitterDefinition emitter, int value) => emitter.mImageFrames = value, static (ref TodEmitterDefinition emitter) => emitter.mImageFrames, static value => value != 1, compiledOffset: 0xC),
            DefinitionMapLoader.Int<TodEmitterDefinition>("Animated", static (ref TodEmitterDefinition emitter, int value) => emitter.mAnimated = value, static (ref TodEmitterDefinition emitter) => emitter.mAnimated, compiledOffset: 0x10),
            DefinitionMapLoader.Flags<TodEmitterDefinition>("ParticleFlags", ParticleFlagSymbols, static (ref TodEmitterDefinition emitter, int bitIndex, bool value) => SetBit(ref emitter.mParticleFlags, bitIndex, value), static (ref TodEmitterDefinition emitter) => emitter.mParticleFlags, compiledOffset: 0x14, compiledSymbols: CompiledParticleFlagSymbols),
            DefinitionMapLoader.Enum<TodEmitterDefinition, EmitterType>("EmitterType", EmitterTypeSymbols, static (ref TodEmitterDefinition emitter, EmitterType value) => emitter.mEmitterType = value, static (ref TodEmitterDefinition emitter) => emitter.mEmitterType, static value => value != EmitterType.Box, compiledOffset: 0x18, compiledSymbols: CompiledEmitterTypeSymbols),
            DefinitionMapLoader.String<TodEmitterDefinition>("Name", static (ref TodEmitterDefinition emitter, string value) => emitter.mName = value, static (ref TodEmitterDefinition emitter) => emitter.mName, compiledOffset: 0x1C),
            DefinitionMapLoader.TrackFloat<TodEmitterDefinition>("SystemDuration", static (ref TodEmitterDefinition emitter) => emitter.mSystemDuration, compiledOffset: 0x24),
            DefinitionMapLoader.String<TodEmitterDefinition>("OnDuration", static (ref TodEmitterDefinition emitter, string value) => emitter.mOnDuration = value, static (ref TodEmitterDefinition emitter) => emitter.mOnDuration, compiledOffset: 0x20),
            DefinitionMapLoader.TrackFloat<TodEmitterDefinition>("CrossFadeDuration", static (ref TodEmitterDefinition emitter) => emitter.mCrossFadeDuration, compiledOffset: 0x2C),
            DefinitionMapLoader.TrackFloat<TodEmitterDefinition>("SpawnRate", static (ref TodEmitterDefinition emitter) => emitter.mSpawnRate, compiledOffset: 0x34),
            DefinitionMapLoader.TrackFloat<TodEmitterDefinition>("SpawnMinActive", static (ref TodEmitterDefinition emitter) => emitter.mSpawnMinActive, compiledOffset: 0x3C),
            DefinitionMapLoader.TrackFloat<TodEmitterDefinition>("SpawnMaxActive", static (ref TodEmitterDefinition emitter) => emitter.mSpawnMaxActive, compiledOffset: 0x44),
            DefinitionMapLoader.TrackFloat<TodEmitterDefinition>("SpawnMaxLaunched", static (ref TodEmitterDefinition emitter) => emitter.mSpawnMaxLaunched, compiledOffset: 0x4C),
            DefinitionMapLoader.TrackFloat<TodEmitterDefinition>("EmitterRadius", static (ref TodEmitterDefinition emitter) => emitter.mEmitterRadius, compiledOffset: 0x54),
            DefinitionMapLoader.TrackFloat<TodEmitterDefinition>("EmitterOffsetX", static (ref TodEmitterDefinition emitter) => emitter.mEmitterOffsetX, compiledOffset: 0x5C),
            DefinitionMapLoader.TrackFloat<TodEmitterDefinition>("EmitterOffsetY", static (ref TodEmitterDefinition emitter) => emitter.mEmitterOffsetY, compiledOffset: 0x64),
            DefinitionMapLoader.TrackFloat<TodEmitterDefinition>("EmitterBoxX", static (ref TodEmitterDefinition emitter) => emitter.mEmitterBoxX, compiledOffset: 0x6C),
            DefinitionMapLoader.TrackFloat<TodEmitterDefinition>("EmitterBoxY", static (ref TodEmitterDefinition emitter) => emitter.mEmitterBoxY, compiledOffset: 0x74),
            DefinitionMapLoader.TrackFloat<TodEmitterDefinition>("EmitterPath", static (ref TodEmitterDefinition emitter) => emitter.mEmitterPath, compiledOffset: 0x8C),
            DefinitionMapLoader.TrackFloat<TodEmitterDefinition>("EmitterSkewX", static (ref TodEmitterDefinition emitter) => emitter.mEmitterSkewX, compiledOffset: 0x7C),
            DefinitionMapLoader.TrackFloat<TodEmitterDefinition>("EmitterSkewY", static (ref TodEmitterDefinition emitter) => emitter.mEmitterSkewY, compiledOffset: 0x84),
            DefinitionMapLoader.TrackFloat<TodEmitterDefinition>("ParticleDuration", static (ref TodEmitterDefinition emitter) => emitter.mParticleDuration, compiledOffset: 0x94),
            DefinitionMapLoader.TrackFloat<TodEmitterDefinition>("SystemRed", static (ref TodEmitterDefinition emitter) => emitter.mSystemRed, compiledOffset: 0xAC),
            DefinitionMapLoader.TrackFloat<TodEmitterDefinition>("SystemGreen", static (ref TodEmitterDefinition emitter) => emitter.mSystemGreen, compiledOffset: 0xB4),
            DefinitionMapLoader.TrackFloat<TodEmitterDefinition>("SystemBlue", static (ref TodEmitterDefinition emitter) => emitter.mSystemBlue, compiledOffset: 0xBC),
            DefinitionMapLoader.TrackFloat<TodEmitterDefinition>("SystemAlpha", static (ref TodEmitterDefinition emitter) => emitter.mSystemAlpha, compiledOffset: 0xC4),
            DefinitionMapLoader.TrackFloat<TodEmitterDefinition>("SystemBrightness", static (ref TodEmitterDefinition emitter) => emitter.mSystemBrightness, compiledOffset: 0xCC),
            DefinitionMapLoader.TrackFloat<TodEmitterDefinition>("LaunchSpeed", static (ref TodEmitterDefinition emitter) => emitter.mLaunchSpeed, compiledOffset: 0x9C),
            DefinitionMapLoader.TrackFloat<TodEmitterDefinition>("LaunchAngle", static (ref TodEmitterDefinition emitter) => emitter.mLaunchAngle, compiledOffset: 0xA4),
            DefinitionMapLoader.Array<TodEmitterDefinition, ParticleField>("Field", ParticleFieldDefMap, static (ref TodEmitterDefinition emitter, ParticleField field) => AddParticleField(ref emitter, field), static (ref TodEmitterDefinition emitter) => SafeCount(emitter.mParticleFields, emitter.mParticleFieldCount), static (ref TodEmitterDefinition emitter, int index) => emitter.mParticleFields[index], compiledOffset: 0xD4),
            DefinitionMapLoader.Array<TodEmitterDefinition, ParticleField>("SystemField", ParticleFieldDefMap, static (ref TodEmitterDefinition emitter, ParticleField field) => AddSystemField(ref emitter, field), static (ref TodEmitterDefinition emitter) => SafeCount(emitter.mSystemFields, emitter.mSystemFieldCount), static (ref TodEmitterDefinition emitter, int index) => emitter.mSystemFields[index], compiledOffset: 0xDC),
            DefinitionMapLoader.TrackFloat<TodEmitterDefinition>("ParticleRed", static (ref TodEmitterDefinition emitter) => emitter.mParticleRed, compiledOffset: 0xE4),
            DefinitionMapLoader.TrackFloat<TodEmitterDefinition>("ParticleGreen", static (ref TodEmitterDefinition emitter) => emitter.mParticleGreen, compiledOffset: 0xEC),
            DefinitionMapLoader.TrackFloat<TodEmitterDefinition>("ParticleBlue", static (ref TodEmitterDefinition emitter) => emitter.mParticleBlue, compiledOffset: 0xF4),
            DefinitionMapLoader.TrackFloat<TodEmitterDefinition>("ParticleAlpha", static (ref TodEmitterDefinition emitter) => emitter.mParticleAlpha, compiledOffset: 0xFC),
            DefinitionMapLoader.TrackFloat<TodEmitterDefinition>("ParticleBrightness", static (ref TodEmitterDefinition emitter) => emitter.mParticleBrightness, compiledOffset: 0x104),
            DefinitionMapLoader.TrackFloat<TodEmitterDefinition>("ParticleSpinAngle", static (ref TodEmitterDefinition emitter) => emitter.mParticleSpinAngle, compiledOffset: 0x10C),
            DefinitionMapLoader.TrackFloat<TodEmitterDefinition>("ParticleSpinSpeed", static (ref TodEmitterDefinition emitter) => emitter.mParticleSpinSpeed, compiledOffset: 0x114),
            DefinitionMapLoader.TrackFloat<TodEmitterDefinition>("ParticleScale", static (ref TodEmitterDefinition emitter) => emitter.mParticleScale, compiledOffset: 0x11C),
            DefinitionMapLoader.TrackFloat<TodEmitterDefinition>("ParticleStretch", static (ref TodEmitterDefinition emitter) => emitter.mParticleStretch, compiledOffset: 0x124),
            DefinitionMapLoader.TrackFloat<TodEmitterDefinition>("CollisionReflect", static (ref TodEmitterDefinition emitter) => emitter.mCollisionReflect, compiledOffset: 0x12C),
            DefinitionMapLoader.TrackFloat<TodEmitterDefinition>("CollisionSpin", static (ref TodEmitterDefinition emitter) => emitter.mCollisionSpin, compiledOffset: 0x134),
            DefinitionMapLoader.TrackFloat<TodEmitterDefinition>("ClipTop", static (ref TodEmitterDefinition emitter) => emitter.mClipTop, compiledOffset: 0x13C),
            DefinitionMapLoader.TrackFloat<TodEmitterDefinition>("ClipBottom", static (ref TodEmitterDefinition emitter) => emitter.mClipBottom, compiledOffset: 0x144),
            DefinitionMapLoader.TrackFloat<TodEmitterDefinition>("ClipLeft", static (ref TodEmitterDefinition emitter) => emitter.mClipLeft, compiledOffset: 0x14C),
            DefinitionMapLoader.TrackFloat<TodEmitterDefinition>("ClipRight", static (ref TodEmitterDefinition emitter) => emitter.mClipRight, compiledOffset: 0x154),
            DefinitionMapLoader.TrackFloat<TodEmitterDefinition>("AnimationRate", static (ref TodEmitterDefinition emitter) => emitter.mAnimationRate, compiledOffset: 0x15C));

        private static readonly DefMap<TodParticleDefinition> ParticleDefMap = new(
            0x8,
            () => new TodParticleDefinition(),
            DefinitionMapLoader.Array<TodParticleDefinition, TodEmitterDefinition>("Emitter", EmitterDefMap, static (ref TodParticleDefinition particles, TodEmitterDefinition emitter) => AddEmitter(ref particles, emitter), static (ref TodParticleDefinition particles) => SafeCount(particles.mEmitterDefs, particles.mEmitterDefCount), static (ref TodParticleDefinition particles, int index) => particles.mEmitterDefs[index], compiledOffset: 0x0));

        public static TodParticleDefinition Decode(Stream stream)
        {
            return DefinitionMapLoader.Load(stream, ParticleDefMap);
        }

        public static void Encode(Stream stream, TodParticleDefinition definition)
        {
            DefinitionMapLoader.Save(stream, ParticleDefMap, definition);
        }

        public static void Encode(Stream stream, TodParticleDefinition definition, string fileName)
        {
            DefinitionMapLoader.Save(stream, ParticleDefMap, definition, fileName);
        }

        public static void WriteXml(Stream stream, TodParticleDefinition definition)
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

        private static int SafeCount<T>(T[] values, int count)
        {
            return values is null ? 0 : Math.Min(values.Length, count);
        }
    }
}
