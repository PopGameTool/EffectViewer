using System;
using System.Collections.Generic;
using System.Globalization;
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
            DefinitionMapLoader.Float<ReanimatorDefinition>("fps", static (ref ReanimatorDefinition reanim, float value) => reanim.mFPS = value),
            new DoScaleField());

        public static ReanimatorDefinition Decode(Stream stream)
        {
            ReanimLoadContext.ScaleType = ReanimScaleType.ScaleFromPC;
            ReanimatorDefinition reanim = DefinitionMapLoader.Load(stream, ReanimatorDefMap);
            ApplyScale(reanim, ReanimLoadContext.ScaleType);
            ReanimLoadContext.ScaleType = ReanimScaleType.ScaleFromPC;
            return reanim;
        }

        private static void ApplyScale(ReanimatorDefinition reanim, ReanimScaleType scaleType)
        {
            if (scaleType != ReanimScaleType.InvertAndScale || reanim.mTracks is null)
            {
                return;
            }

            for (int i = 0; i < reanim.mTrackCount; i++)
            {
                ReanimatorTrack track = reanim.mTracks[i];
                bool isGround = ReanimatorXnaHelpers.ReanimatorTrackNameToId(track.mName) == Reanimation.ReanimTrackId__ground;
                if (isGround || track.mTransforms is null)
                {
                    continue;
                }

                for (int j = 0; j < track.mTransformCount; j++)
                {
                    ReanimatorTransform transform = track.mTransforms[j];
                    if (transform.mTransX != ReanimatorXnaHelpers.DEFAULT_FIELD_PLACEHOLDER)
                    {
                        transform.mTransX *= 1.875f;
                    }

                    if (transform.mTransY != ReanimatorXnaHelpers.DEFAULT_FIELD_PLACEHOLDER)
                    {
                        transform.mTransY *= 1.875f;
                    }

                    track.mTransforms[j] = transform;
                }
            }
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

        private sealed class DoScaleField : IDefField<ReanimatorDefinition>
        {
            public string Name => "doScale";
            public DefFieldType FieldType => DefFieldType.Int;

            public bool TryRead(SexyXmlParser parser, string elementName, ref ReanimatorDefinition definition)
            {
                if (!string.Equals(elementName, Name, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                string value = ReadElementText(parser);
                if (sbyte.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out sbyte parsed))
                {
                    ReanimLoadContext.ScaleType = (ReanimScaleType)(byte)parsed;
                    return true;
                }

                throw parser.CreateError($"Can't parse int value '{value}'");
            }

            private static string ReadElementText(SexyXmlParser parser)
            {
                if (!parser.TryNextElement(out SexyXmlElement element))
                {
                    throw parser.CreateError("Missing element value");
                }

                if (element.Type == SexyXmlElementType.End)
                {
                    return string.Empty;
                }

                if (element.Type != SexyXmlElementType.Element)
                {
                    throw parser.CreateError("unknown element type");
                }

                string value = element.Value;
                if (!parser.TryNextElement(out element) || element.Type != SexyXmlElementType.End)
                {
                    throw parser.CreateError("Missing element end");
                }

                return value;
            }
        }

        private static class ReanimLoadContext
        {
            [ThreadStatic]
            public static ReanimScaleType ScaleType;
        }

        internal enum ReanimScaleType
        {
            NoScale,
            InvertAndScale,
            ScaleFromPC = 0xFF
        }
    }
}
