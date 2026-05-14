namespace EffectViewer.EffectRuntime.Trail
{
    public class TrailParams
    {
        public TrailType mTrailType;
        public string mTrailFileName;

        public TrailParams(TrailType aTrailType, string aTrailFileName)
        {
            mTrailType = aTrailType;
            mTrailFileName = aTrailFileName;
        }
    }
}