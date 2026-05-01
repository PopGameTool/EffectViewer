using System;
using System.Collections.Generic;
using System.IO;

namespace EffectViewer.TodLib.Reanim
{
    public class ReanimReader
    {
        private static readonly DefMap<ReanimatorTransform> ReanimatorTransformDefMap = new(
            0x2C,
            () => new ReanimatorTransform(),
            DefinitionMapLoader.Float<ReanimatorTransform>("x", static (ref ReanimatorTransform transform, float value) => transform.mTransX = value, static (ref ReanimatorTransform transform) => transform.mTransX, IsReanimValueSet, compiledOffset: 0x0),
            DefinitionMapLoader.Float<ReanimatorTransform>("y", static (ref ReanimatorTransform transform, float value) => transform.mTransY = value, static (ref ReanimatorTransform transform) => transform.mTransY, IsReanimValueSet, compiledOffset: 0x4),
            DefinitionMapLoader.Float<ReanimatorTransform>("kx", static (ref ReanimatorTransform transform, float value) => transform.mSkewX = value, static (ref ReanimatorTransform transform) => transform.mSkewX, IsReanimValueSet, compiledOffset: 0x8),
            DefinitionMapLoader.Float<ReanimatorTransform>("ky", static (ref ReanimatorTransform transform, float value) => transform.mSkewY = value, static (ref ReanimatorTransform transform) => transform.mSkewY, IsReanimValueSet, compiledOffset: 0xC),
            DefinitionMapLoader.Float<ReanimatorTransform>("sx", static (ref ReanimatorTransform transform, float value) => transform.mScaleX = value, static (ref ReanimatorTransform transform) => transform.mScaleX, IsReanimValueSet, compiledOffset: 0x10),
            DefinitionMapLoader.Float<ReanimatorTransform>("sy", static (ref ReanimatorTransform transform, float value) => transform.mScaleY = value, static (ref ReanimatorTransform transform) => transform.mScaleY, IsReanimValueSet, compiledOffset: 0x14),
            DefinitionMapLoader.Float<ReanimatorTransform>("f", static (ref ReanimatorTransform transform, float value) => transform.mFrame = value, static (ref ReanimatorTransform transform) => transform.mFrame, IsReanimValueSet, compiledOffset: 0x18),
            DefinitionMapLoader.Float<ReanimatorTransform>("a", static (ref ReanimatorTransform transform, float value) => transform.mAlpha = value, static (ref ReanimatorTransform transform) => transform.mAlpha, IsReanimValueSet, compiledOffset: 0x1C),
            DefinitionMapLoader.Image<ReanimatorTransform>("i", static (ref ReanimatorTransform transform, string value) => transform.mImage = value, static (ref ReanimatorTransform transform) => transform.mImage, compiledOffset: 0x20),
            DefinitionMapLoader.Font<ReanimatorTransform>("font", static (ref ReanimatorTransform transform, string value) => transform.mFont = value, static (ref ReanimatorTransform transform) => transform.mFont, compiledOffset: 0x24),
            DefinitionMapLoader.String<ReanimatorTransform>("text", static (ref ReanimatorTransform transform, string value) => transform.mText = value, static (ref ReanimatorTransform transform) => transform.mText, compiledOffset: 0x28));

        private static readonly DefMap<ReanimatorTrack> ReanimatorTrackDefMap = new(
            0xC,
            () => new ReanimatorTrack(),
            DefinitionMapLoader.String<ReanimatorTrack>("name", static (ref ReanimatorTrack track, string value) => track.mName = Reanimation.ToLower(value), static (ref ReanimatorTrack track) => track.mName, compiledOffset: 0x0),
            DefinitionMapLoader.Array<ReanimatorTrack, ReanimatorTransform>("t", ReanimatorTransformDefMap, static (ref ReanimatorTrack track, ReanimatorTransform transform) => AddTransform(ref track, transform), static (ref ReanimatorTrack track) => SafeCount(track.mTransforms, track.mTransformCount), static (ref ReanimatorTrack track, int index) => track.mTransforms[index], compiledOffset: 0x4));

        private static readonly DefMap<ReanimatorDefinition> ReanimatorDefMap = new(
            0x10,
            () => new ReanimatorDefinition(),
            DefinitionMapLoader.Array<ReanimatorDefinition, ReanimatorTrack>("track", ReanimatorTrackDefMap, static (ref ReanimatorDefinition reanim, ReanimatorTrack track) => AddTrack(ref reanim, track), static (ref ReanimatorDefinition reanim) => SafeCount(reanim.mTracks, reanim.mTrackCount), static (ref ReanimatorDefinition reanim, int index) => reanim.mTracks[index], compiledOffset: 0x0),
            DefinitionMapLoader.Float<ReanimatorDefinition>("fps", static (ref ReanimatorDefinition reanim, float value) => reanim.mFPS = value, static (ref ReanimatorDefinition reanim) => reanim.mFPS, static value => value != 12f, compiledOffset: 0x8));

        public static ReanimatorDefinition Decode(Stream stream)
        {
            return DefinitionMapLoader.Load(stream, ReanimatorDefMap);
        }

        public static void Encode(Stream stream, ReanimatorDefinition definition)
        {
            DefinitionMapLoader.Save(stream, ReanimatorDefMap, definition);
        }

        public static void Encode(Stream stream, ReanimatorDefinition definition, string fileName)
        {
            DefinitionMapLoader.Save(stream, ReanimatorDefMap, definition, fileName);
        }

        public static void WriteXml(Stream stream, ReanimatorDefinition definition)
        {
            Encode(stream, definition);
        }

        private static void AddTrack(ref ReanimatorDefinition reanim, ReanimatorTrack track)
        {
            ReanimatorTrack[] tracks = reanim.mTracks ?? [];
            Array.Resize(ref tracks, tracks.Length + 1);
            tracks[^1] = track;
            reanim.mTracks = tracks;
            reanim.mTrackCount = (short)tracks.Length;
        }

        private static void AddTransform(ref ReanimatorTrack track, ReanimatorTransform transform)
        {
            ReanimatorTransform[] transforms = track.mTransforms ?? [];
            Array.Resize(ref transforms, transforms.Length + 1);
            transforms[^1] = transform;
            track.mTransforms = transforms;
            track.mTransformCount = (short)transforms.Length;
        }

        private static bool IsReanimValueSet(float value)
        {
            return value != ReanimatorXnaHelpers.DEFAULT_FIELD_PLACEHOLDER;
        }

        private static int SafeCount<T>(T[] values, int count)
        {
            return values is null ? 0 : Math.Min(values.Length, count);
        }
    }
}
