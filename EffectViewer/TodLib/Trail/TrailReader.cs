using System;
using System.Collections.Generic;
using System.IO;

namespace EffectViewer.TodLib.Trail
{
    public static class TrailReader
    {
        private static readonly IReadOnlyDictionary<string, int> TrailFlagSymbols =
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["Loops"] = 0
            };

        private static readonly DefMap<TrailDefinition> TrailDefMap = new(
            () => new TrailDefinition(),
            DefinitionMapLoader.Image<TrailDefinition>("Image", static (ref TrailDefinition trail, string value) => trail.mImage = value, static (ref TrailDefinition trail) => trail.mImage),
            DefinitionMapLoader.Int<TrailDefinition>("MaxPoints", static (ref TrailDefinition trail, int value) => trail.mMaxPoints = value, static (ref TrailDefinition trail) => trail.mMaxPoints, static value => value != 2),
            DefinitionMapLoader.Float<TrailDefinition>("MinPointDistance", static (ref TrailDefinition trail, float value) => trail.mMinPointDistance = value, static (ref TrailDefinition trail) => trail.mMinPointDistance, static value => value != 1f),
            DefinitionMapLoader.Flags<TrailDefinition>("TrailFlags", TrailFlagSymbols, static (ref TrailDefinition trail, int bitIndex, bool value) => SexyParticleReader.SetBit(ref trail.mTrailFlags, bitIndex, value), static (ref TrailDefinition trail) => trail.mTrailFlags),
            DefinitionMapLoader.TrackFloat<TrailDefinition>("WidthOverLength", static (ref TrailDefinition trail) => trail.mWidthOverLength, 1f),
            DefinitionMapLoader.TrackFloat<TrailDefinition>("WidthOverTime", static (ref TrailDefinition trail) => trail.mWidthOverTime, 1f),
            DefinitionMapLoader.TrackFloat<TrailDefinition>("AlphaOverLength", static (ref TrailDefinition trail) => trail.mAlphaOverLength, 1f),
            DefinitionMapLoader.TrackFloat<TrailDefinition>("AlphaOverTime", static (ref TrailDefinition trail) => trail.mAlphaOverTime, 1f),
            DefinitionMapLoader.TrackFloat<TrailDefinition>("TrailDuration", static (ref TrailDefinition trail) => trail.mTrailDuration, 100f));

        public static TrailDefinition Decode(Stream stream)
        {
            return DefinitionMapLoader.Load(stream, TrailDefMap);
        }

        public static void Encode(Stream stream, TrailDefinition definition)
        {
            DefinitionMapLoader.Save(stream, TrailDefMap, definition);
        }

        public static void WriteXml(Stream stream, TrailDefinition definition)
        {
            Encode(stream, definition);
        }
    }
}
