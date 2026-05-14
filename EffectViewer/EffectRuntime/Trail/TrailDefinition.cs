namespace EffectViewer.EffectRuntime.Trail
{
    public class TrailDefinition
    {
        public string mImage;
        public int mMaxPoints;
        public float mMinPointDistance;
        public int mTrailFlags;
        public readonly FloatParameterTrack mTrailDuration = new();
        public readonly FloatParameterTrack mWidthOverLength = new();
        public readonly FloatParameterTrack mWidthOverTime = new();
        public readonly FloatParameterTrack mAlphaOverLength = new();
        public readonly FloatParameterTrack mAlphaOverTime = new();

        public TrailDefinition()
        {
            mMaxPoints = 2;
            mMinPointDistance = 1f;
            mTrailFlags = 0;
            mImage = null;
        }

        public void Dispose()
        {
        }

        public void ApplyDefaults()
        {
            Definition.FloatTrackSetDefault(mWidthOverLength, 1f);
            Definition.FloatTrackSetDefault(mWidthOverTime, 1f);
            Definition.FloatTrackSetDefault(mTrailDuration, 100f);
            Definition.FloatTrackSetDefault(mAlphaOverLength, 1f);
            Definition.FloatTrackSetDefault(mAlphaOverTime, 1f);
        }
    }
}
