namespace EffectViewer.EffectRuntime.Common
{
    public static class RandomNumbers
    {
        public static MTRand gMTRand = new MTRand();

        public static int Rand()
        {
            return (int)gMTRand.Next();
        }

        public static int Rand(int range)
        {
            return (int)gMTRand.Next((uint)range);
        }

        public static float Rand(float range)
        {
            return gMTRand.Next(range);
        }

        public static void SRand(uint theSeed)
        {
            gMTRand.SRand(theSeed);
        }
    }
}