using System;
using System.Collections.Generic;
using System.IO;

namespace EffectViewer.TodLib.Reanim
{
    public class ReanimReader
    {
        private static readonly DefMap<ReanimatorTransform> ReanimatorTransformDefMap = new(
            () => new ReanimatorTransform(),
            DefinitionMapLoader.Float<ReanimatorTransform>("x", static (ref ReanimatorTransform transform, float value) => transform.mTransX = value, static (ref ReanimatorTransform transform) => transform.mTransX, IsReanimValueSet),
            DefinitionMapLoader.Float<ReanimatorTransform>("y", static (ref ReanimatorTransform transform, float value) => transform.mTransY = value, static (ref ReanimatorTransform transform) => transform.mTransY, IsReanimValueSet),
            DefinitionMapLoader.Float<ReanimatorTransform>("kx", static (ref ReanimatorTransform transform, float value) => transform.mSkewX = value, static (ref ReanimatorTransform transform) => transform.mSkewX, IsReanimValueSet),
            DefinitionMapLoader.Float<ReanimatorTransform>("ky", static (ref ReanimatorTransform transform, float value) => transform.mSkewY = value, static (ref ReanimatorTransform transform) => transform.mSkewY, IsReanimValueSet),
            DefinitionMapLoader.Float<ReanimatorTransform>("sx", static (ref ReanimatorTransform transform, float value) => transform.mScaleX = value, static (ref ReanimatorTransform transform) => transform.mScaleX, IsReanimValueSet),
            DefinitionMapLoader.Float<ReanimatorTransform>("sy", static (ref ReanimatorTransform transform, float value) => transform.mScaleY = value, static (ref ReanimatorTransform transform) => transform.mScaleY, IsReanimValueSet),
            DefinitionMapLoader.Float<ReanimatorTransform>("f", static (ref ReanimatorTransform transform, float value) => transform.mFrame = value, static (ref ReanimatorTransform transform) => transform.mFrame, IsReanimValueSet),
            DefinitionMapLoader.Float<ReanimatorTransform>("a", static (ref ReanimatorTransform transform, float value) => transform.mAlpha = value, static (ref ReanimatorTransform transform) => transform.mAlpha, IsReanimValueSet),
            DefinitionMapLoader.Image<ReanimatorTransform>("i", static (ref ReanimatorTransform transform, string value) => transform.mImage = value, static (ref ReanimatorTransform transform) => transform.mImage),
            DefinitionMapLoader.Font<ReanimatorTransform>("font", static (ref ReanimatorTransform transform, string value) => transform.mFont = value, static (ref ReanimatorTransform transform) => transform.mFont),
            DefinitionMapLoader.String<ReanimatorTransform>("text", static (ref ReanimatorTransform transform, string value) => transform.mText = value, static (ref ReanimatorTransform transform) => transform.mText));

        private static readonly DefMap<ReanimatorTrack> ReanimatorTrackDefMap = new(
            () => new ReanimatorTrack(),
            DefinitionMapLoader.String<ReanimatorTrack>("name", static (ref ReanimatorTrack track, string value) => track.mName = Reanimation.ToLower(value), static (ref ReanimatorTrack track) => track.mName),
            DefinitionMapLoader.Array<ReanimatorTrack, ReanimatorTransform>("t", ReanimatorTransformDefMap, static (ref ReanimatorTrack track, ReanimatorTransform transform) => AddTransform(ref track, transform), static (ref ReanimatorTrack track) => SafeCount(track.mTransforms, track.mTransformCount), static (ref ReanimatorTrack track, int index) => track.mTransforms[index]));

        private static readonly DefMap<ReanimatorDefinition> ReanimatorDefMap = new(
            () => new ReanimatorDefinition(),
            DefinitionMapLoader.Array<ReanimatorDefinition, ReanimatorTrack>("track", ReanimatorTrackDefMap, static (ref ReanimatorDefinition reanim, ReanimatorTrack track) => AddTrack(ref reanim, track), static (ref ReanimatorDefinition reanim) => SafeCount(reanim.mTracks, reanim.mTrackCount), static (ref ReanimatorDefinition reanim, int index) => reanim.mTracks[index]),
            DefinitionMapLoader.Float<ReanimatorDefinition>("fps", static (ref ReanimatorDefinition reanim, float value) => reanim.mFPS = value, static (ref ReanimatorDefinition reanim) => reanim.mFPS, static value => value != 12f));

        public static ReanimatorDefinition Decode(Stream stream)
        {
            return DefinitionMapLoader.Load(stream, ReanimatorDefMap);
        }

        public static void Encode(Stream stream, ReanimatorDefinition definition)
        {
            DefinitionMapLoader.Save(stream, ReanimatorDefMap, definition);
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
