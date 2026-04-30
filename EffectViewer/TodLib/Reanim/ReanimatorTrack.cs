namespace EffectViewer.TodLib.Reanim
{
    public class ReanimatorTrack
    {
        public string mName;
        public ReanimatorTransform[] mTransforms;
        public short mTransformCount;

        public ReanimatorTrack(string name, int transformCount)
        {
            mName = name;
            mTransformCount = (short)transformCount;
            mTransforms = new ReanimatorTransform[mTransformCount];
        }

        public override string ToString()
        {
            return mName;
        }
    }
}