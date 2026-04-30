namespace EffectViewer.TodLib.Reanim
{
    public class ReanimationParams
    {
        public string mReanimationType;
        public string mReanimFileName;
        public int mReanimParamFlags;

        public ReanimationParams(string aReanimationType, string aReanimFilename) : this(aReanimationType, aReanimFilename, 0)
        {
        }

        public ReanimationParams(string aReanimationType, string aReanimFilename, int aReanimparamFlags)
        {
            mReanimationType = aReanimationType;
            mReanimFileName = aReanimFilename;
            mReanimParamFlags = aReanimparamFlags;
        }
    }
}