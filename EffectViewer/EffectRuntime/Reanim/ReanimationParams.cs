namespace EffectViewer.EffectRuntime.Reanim
{
    public class ReanimationParams
    {
        public string mReanimationType;
        public string mReanimFileName;
        public string mResolvedFileName;
        public int mReanimParamFlags;

        public ReanimationParams(string aReanimationType, string aReanimFilename) : this(aReanimationType, aReanimFilename, 0)
        {
        }

        public ReanimationParams(string aReanimationType, string aReanimFilename, int aReanimparamFlags)
            : this(aReanimationType, aReanimFilename, aReanimFilename, aReanimparamFlags)
        {
        }

        public ReanimationParams(string aReanimationType, string aReanimFilename, string aResolvedFilename) : this(aReanimationType, aReanimFilename, aResolvedFilename, 0)
        {
        }

        public ReanimationParams(string aReanimationType, string aReanimFilename, string aResolvedFilename, int aReanimparamFlags)
        {
            mReanimationType = aReanimationType;
            mReanimFileName = aReanimFilename;
            mResolvedFileName = string.IsNullOrWhiteSpace(aResolvedFilename) ? aReanimFilename : aResolvedFilename;
            mReanimParamFlags = aReanimparamFlags;
        }
    }
}
