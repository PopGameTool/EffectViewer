using System.IO;

namespace EffectViewer.EffectRuntime.Trail
{
    public static class TrailUtility
    {
        public static int gTrailDefCount;
        public static TrailDefinition[] gTrailDefArray;
        public static int gTrailParamArraySize;
        public static TrailParams[] gTrailParamArray;

        public static void TrailLoadDefinitions(TrailParams[] theTrailParamArray, int theTrailParamArraySize)
        {
            gTrailParamArraySize = theTrailParamArraySize;
            gTrailParamArray = theTrailParamArray;
            gTrailDefCount = theTrailParamArraySize;
            gTrailDefArray = new TrailDefinition[gTrailDefCount];

            for (int i = 0; i < gTrailParamArraySize; i++)
            {
                TrailParams aTrailParams = theTrailParamArray[i];
                Debug.Assert(aTrailParams.mTrailType == (TrailType)i);
                if (!TrailLoadADef(out gTrailDefArray[i], aTrailParams.mTrailFileName))
                {
                    Debug.Log(DebugType.Error, $"Failed to load trail '{aTrailParams}'");
                }
            }
        }

        public static void TrailFreeDefinitions()
        {
            gTrailDefArray = null;
            gTrailDefCount = 0;
            gTrailParamArray = null;
            gTrailParamArraySize = 0;
        }

        public static bool TrailLoadADef(out TrailDefinition theTrailDef, string theTrailFileName)
        {
            using (Stream fileStream = File.OpenRead(theTrailFileName))
            {
                theTrailDef = TrailReader.Decode(fileStream);
            }

            theTrailDef.ApplyDefaults();
            return true;
        }
    }
}
