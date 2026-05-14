using System;
using System.Runtime.CompilerServices;

namespace EffectViewer.EffectRuntime.Graphics
{
    public class Graphics
    {
        public float mTransX;
        public float mTransY;
        public DrawMode mDrawMode;
        public EffectColor mColor;
        public bool mColorizeImages;
        public Rectangle mClipRect;

        public EffectColor GetColor()
        {
            return mColor;
        }

        public Rectangle GetClipRect()
        {
            return mClipRect;
        }

        public bool GetColorizeImages()
        {
            return mColorizeImages;
        }

        public DrawMode GetDrawMode()
        {
            return mDrawMode;
        }

        public void SetColor(in EffectColor color)
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
