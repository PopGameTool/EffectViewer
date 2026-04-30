namespace EffectViewer.TodLib.Graphics
{
    public class Image
    {
        public string mId;
        public int mWidth;
        public int mHeight;
        public int mNumCols;
        public int mNumRows;
        public object mPlatformImage;

        public int GetCelWidth()
        {
            return mNumCols <= 0 ? mWidth : mWidth / mNumCols;
        }

        public int GetCelHeight()
        {
            return mNumRows <= 0 ? mHeight : mHeight / mNumRows;
        }
    }
}
