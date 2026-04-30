namespace EffectViewer.TodLib.Reanim
{
    public class ReanimationHolder
    {
        public readonly DataArray<Reanimation, ReanimationID> mReanimations = new();

        public void Dispose()
        {
            DisposeHolder();
        }

        public void InitializeHolder()
        {
            mReanimations.DataArrayInitialize(1024U, "reanims");
        }

        public void DisposeHolder()
        {
            mReanimations.DataArrayDispose();
        }

        public Reanimation AllocReanimation(float theX, float theY, int theRenderOrder, string theReanimationType)
        {
            Debug.ASSERT(mReanimations.mSize != mReanimations.mMaxSize);
            Reanimation aReanim = mReanimations.DataArrayAlloc();
            aReanim.mReanimationHolder = this;
            aReanim.mRenderOrder = theRenderOrder;
            aReanim.ReanimationInitializeType(theX, theY, theReanimationType);
            return aReanim;
        }
    }
}