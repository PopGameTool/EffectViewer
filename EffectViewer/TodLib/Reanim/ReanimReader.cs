using System;
using System.Collections.Generic;
using System.IO;

namespace EffectViewer.TodLib.Reanim
{
    public class ReanimReader
    {
        private static readonly DefMap<ReanimatorTransform> ReanimatorTransformDefMap = new(
            () => new ReanimatorTransform(),
            DefinitionMapLoader.Float<ReanimatorTransform>("x", static (ref ReanimatorTransform transform, float value) => transform.mTransX = value),
            DefinitionMapLoader.Float<ReanimatorTransform>("y", static (ref ReanimatorTransform transform, float value) => transform.mTransY = value),
            DefinitionMapLoader.Float<ReanimatorTransform>("kx", static (ref ReanimatorTransform transform, float value) => transform.mSkewX = value),
            DefinitionMapLoader.Float<ReanimatorTransform>("ky", static (ref ReanimatorTransform transform, float value) => transform.mSkewY = value),
            DefinitionMapLoader.Float<ReanimatorTransform>("sx", static (ref ReanimatorTransform transform, float value) => transform.mScaleX = value),
            DefinitionMapLoader.Float<ReanimatorTransform>("sy", static (ref ReanimatorTransform transform, float value) => transform.mScaleY = value),
            DefinitionMapLoader.Float<ReanimatorTransform>("f", static (ref ReanimatorTransform transform, float value) => transform.mFrame = value),
            DefinitionMapLoader.Float<ReanimatorTransform>("a", static (ref ReanimatorTransform transform, float value) => transform.mAlpha = value),
            DefinitionMapLoader.Image<ReanimatorTransform>("i", static (ref ReanimatorTransform transform, string value) => transform.mImage = value),
            DefinitionMapLoader.Font<ReanimatorTransform>("font", static (ref ReanimatorTransform transform, string value) => transform.mFont = value),
            DefinitionMapLoader.String<ReanimatorTransform>("text", static (ref ReanimatorTransform transform, string value) => transform.mText = value));

        private static readonly DefMap<ReanimatorTrack> ReanimatorTrackDefMap = new(
            () => new ReanimatorTrack(),
            DefinitionMapLoader.String<ReanimatorTrack>("name", static (ref ReanimatorTrack track, string value) => track.mName = Reanimation.ToLower(value)),
            DefinitionMapLoader.Array<ReanimatorTrack, ReanimatorTransform>("t", ReanimatorTransformDefMap, static (ref ReanimatorTrack track, ReanimatorTransform transform) => AddTransform(ref track, transform)));

        private static readonly DefMap<ReanimatorDefinition> ReanimatorDefMap = new(
            () => new ReanimatorDefinition(),
            DefinitionMapLoader.Array<ReanimatorDefinition, ReanimatorTrack>("track", ReanimatorTrackDefMap, static (ref ReanimatorDefinition reanim, ReanimatorTrack track) => AddTrack(ref reanim, track)),
            DefinitionMapLoader.Float<ReanimatorDefinition>("fps", static (ref ReanimatorDefinition reanim, float value) => reanim.mFPS = value));

        public static ReanimatorDefinition Decode(Stream stream)
        {
            return DefinitionMapLoader.Load(stream, ReanimatorDefMap);
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
    }
}
