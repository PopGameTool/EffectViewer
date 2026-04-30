using System;
using System.Runtime.CompilerServices;

namespace EffectViewer.TodLib.Graphics
{
    public class Graphics
    {
        public float mTransX;
        public float mTransY;
        public DrawMode mDrawMode;
        public SexyColor mColor;
        public Rectangle mClipRect;

        public SexyColor GetColor()
        {
            return mColor;
        }

        public Rectangle GetClipRect()
        {
            return mClipRect;
        }

        public bool GetColorizeImages()
        {
            return false;
        }

        public DrawMode GetDrawMode()
        {
            return mDrawMode;
        }

        public void SetColor(in SexyColor color)
        {
            mColor = color;
        }

        public void SetDrawMode(DrawMode drawMode)
        {
            mDrawMode = drawMode;
        }

        public virtual void FillRect(Rectangle rect)
        {

        }

        public virtual void FillRect(int x, int y, int width, int height)
        {

        }

        public virtual void DrawTrianglesTex(Image theTexture, ReadOnlySpan<InlineArray3<TriVertex>> theVertices)
        {

        }
    }
}
