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

        public virtual void DrawImageMatrix(
            Image theImage,
            in Matrix4x4 theTransform,
            in Rectangle theClipRect,
            in EffectColor theColor,
            DrawMode theDrawMode,
            in Rectangle theSrcRect)
        {
            if (theImage == null ||
                theSrcRect.Width <= 0 ||
                theSrcRect.Height <= 0 ||
                theClipRect.Width <= 0 ||
                theClipRect.Height <= 0 ||
                theColor.mAlpha <= 0)
            {
                return;
            }

            float halfWidth = theSrcRect.Width * 0.5f;
            float halfHeight = theSrcRect.Height * 0.5f;
            float left = -halfWidth;
            float top = -halfHeight;
            float right = halfWidth;
            float bottom = halfHeight;

            Vector2 topLeft = Vector2.Transform(new Vector2(left, top), theTransform);
            Vector2 topRight = Vector2.Transform(new Vector2(right, top), theTransform);
            Vector2 bottomRight = Vector2.Transform(new Vector2(right, bottom), theTransform);
            Vector2 bottomLeft = Vector2.Transform(new Vector2(left, bottom), theTransform);

            float textureWidth = Math.Max(1, theImage.mWidth);
            float textureHeight = Math.Max(1, theImage.mHeight);
            float u0 = theSrcRect.Left / textureWidth;
            float v0 = theSrcRect.Top / textureHeight;
            float u1 = theSrcRect.Right / textureWidth;
            float v1 = theSrcRect.Bottom / textureHeight;

            InlineArray3<TriVertex> first = new();
            first[0] = BuildTriVertex(topLeft, u0, v0, theColor);
            first[1] = BuildTriVertex(topRight, u1, v0, theColor);
            first[2] = BuildTriVertex(bottomRight, u1, v1, theColor);

            InlineArray3<TriVertex> second = new();
            second[0] = BuildTriVertex(topLeft, u0, v0, theColor);
            second[1] = BuildTriVertex(bottomRight, u1, v1, theColor);
            second[2] = BuildTriVertex(bottomLeft, u0, v1, theColor);

            InlineArray2<InlineArray3<TriVertex>> triangles = new();
            triangles[0] = first;
            triangles[1] = second;

            float oldTransX = mTransX;
            float oldTransY = mTransY;
            Rectangle oldClipRect = mClipRect;
            EffectColor oldColor = mColor;
            bool oldColorizeImages = mColorizeImages;
            DrawMode oldMode = mDrawMode;
            try
            {
                mTransX = 0f;
                mTransY = 0f;
                mClipRect = theClipRect;
                mColor = EffectColor.White;
                mColorizeImages = false;
                mDrawMode = theDrawMode;
                DrawTrianglesTex(theImage, ((Span<InlineArray3<TriVertex>>)triangles)[..2]);
            }
            finally
            {
                mTransX = oldTransX;
                mTransY = oldTransY;
                mClipRect = oldClipRect;
                mColor = oldColor;
                mColorizeImages = oldColorizeImages;
                mDrawMode = oldMode;
            }
        }

        private static TriVertex BuildTriVertex(Vector2 position, float u, float v, in EffectColor color)
        {
            return new TriVertex
            {
                Position = new Vector3(position, 0f),
                TextureCoordinate = new Vector2(u, v),
                Color = color
            };
        }
    }
}
