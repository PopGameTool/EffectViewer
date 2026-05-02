using System.Numerics;

namespace EffectViewer.Rendering
{
    public readonly record struct ViewportTransformBox(
        Vector2 TopLeft,
        Vector2 TopRight,
        Vector2 BottomRight,
        Vector2 BottomLeft)
    {
        public Vector2 Center => (TopLeft + TopRight + BottomRight + BottomLeft) * 0.25f;
        public Vector2 TopCenter => (TopLeft + TopRight) * 0.5f;
        public Vector2 RightCenter => (TopRight + BottomRight) * 0.5f;
        public Vector2 BottomCenter => (BottomLeft + BottomRight) * 0.5f;
        public Vector2 LeftCenter => (TopLeft + BottomLeft) * 0.5f;
    }
}
