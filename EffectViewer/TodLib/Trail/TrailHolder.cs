namespace EffectViewer.TodLib.Trail
{
    public class TrailHolder
    {
        public readonly DataArray<Trail, TrailID> mTrails = new();

        public void Dispose()
        {
            DisposeHolder();
        }

        public void InitializeHolder()
        {
            mTrails.DataArrayInitialize(1024U, "trails");
        }

        public void DisposeHolder()
        {
            mTrails.DataArrayDispose();
        }

        public Trail AllocTrail(int theRenderOrder, TrailType theTrailType)
        {
            TrailDefinition theDefinition = GlobalMembersTrail.gTrailDefArray[(int)theTrailType];
            return AllocTrailFromDef(theRenderOrder, theDefinition);
        }

        public Trail AllocTrailFromDef(int theRenderOrder, TrailDefinition theDefinition)
        {
            if (mTrails.mSize == mTrails.mMaxSize)
            {
                return null;
            }

            Trail aTrail = mTrails.DataArrayAlloc();
            aTrail.mTrailHolder = this;
            aTrail.mDefinition = theDefinition;

            float aDurationInterp = TodCommon.RandRangeFloat(0f, 1f);
            aTrail.mTrailDuration = (int)Definition.FloatTrackEvaluate(aTrail.mDefinition.mTrailDuration, 0f, aDurationInterp);
            return aTrail;
        }
    }
}