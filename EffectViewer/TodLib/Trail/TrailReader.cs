using System;
using System.Collections.Generic;
using System.IO;

namespace EffectViewer.TodLib.Trail
{
    internal class TrailReader
    {
        private static readonly IReadOnlyDictionary<string, int> TrailFlagSymbols =
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["Loops"] = 0
            };

        private static readonly DefMap<TrailDefinition> TrailDefMap = new(
            () => new TrailDefinition(),
            DefinitionMapLoader.Image<TrailDefinition>("Image", static (ref TrailDefinition trail, string value) => trail.mImage = value),
            DefinitionMapLoader.Int<TrailDefinition>("MaxPoints", static (ref TrailDefinition trail, int value) => trail.mMaxPoints = value),
            DefinitionMapLoader.Float<TrailDefinition>("MinPointDistance", static (ref TrailDefinition trail, float value) => trail.mMinPointDistance = value),
            DefinitionMapLoader.Flags<TrailDefinition>("TrailFlags", TrailFlagSymbols, static (ref TrailDefinition trail, int bitIndex, bool value) => SexyParticleReader.SetBit(ref trail.mTrailFlags, bitIndex, value)),
            DefinitionMapLoader.TrackFloat<TrailDefinition>("WidthOverLength", static (ref TrailDefinition trail) => trail.mWidthOverLength),
            DefinitionMapLoader.TrackFloat<TrailDefinition>("WidthOverTime", static (ref TrailDefinition trail) => trail.mWidthOverTime),
            DefinitionMapLoader.TrackFloat<TrailDefinition>("AlphaOverLength", static (ref TrailDefinition trail) => trail.mAlphaOverLength),
            DefinitionMapLoader.TrackFloat<TrailDefinition>("AlphaOverTime", static (ref TrailDefinition trail) => trail.mAlphaOverTime),
            DefinitionMapLoader.TrackFloat<TrailDefinition>("TrailDuration", static (ref TrailDefinition trail) => trail.mTrailDuration));

        public static TrailDefinition Decode(Stream stream)
        {
            return DefinitionMapLoader.Load(stream, TrailDefMap);
        }
    }
}
