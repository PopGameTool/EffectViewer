using System;

namespace EffectViewer.Rendering.Export
{
    internal struct PreviewFrameBounds
    {
        public float Left { get; private set; }
        public float Top { get; private set; }
        public float Right { get; private set; }
        public float Bottom { get; private set; }
        public bool HasContent { get; private set; }
        public readonly float Width => HasContent ? Math.Max(0f, Right - Left) : 0f;
        public readonly float Height => HasContent ? Math.Max(0f, Bottom - Top) : 0f;

        public void Include(float x, float y)
        {
            if (!float.IsFinite(x) || !float.IsFinite(y))
            {
                return;
            }

            if (!HasContent)
            {
                Left = Right = x;
                Top = Bottom = y;
                HasContent = true;
                return;
            }

            Left = Math.Min(Left, x);
            Top = Math.Min(Top, y);
            Right = Math.Max(Right, x);
            Bottom = Math.Max(Bottom, y);
        }

        public void Include(float left, float top, float right, float bottom)
        {
            Include(left, top);
            Include(right, bottom);
        }

        public void Include(PreviewFrameBounds bounds)
        {
            if (!bounds.HasContent)
            {
                return;
            }

            Include(bounds.Left, bounds.Top, bounds.Right, bounds.Bottom);
        }
    }
}

