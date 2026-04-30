using System.Numerics;
using EffectViewer.TodLib.Common;
using EffectViewer.TodLib.Trail;

namespace EffectViewer.Runtime.Showcase
{
    public sealed class ShowcaseTrail
    {
        private readonly float _baseX;
        private readonly float _baseY;
        private int _tick;
        private bool _manualPoints;
        private bool _attached;

        internal ShowcaseTrail(ShowcaseScene scene, string id, Trail trail, float x, float y)
        {
            id_ = id;
            Trail = trail;
            _baseX = x;
            _baseY = y;
        }

        internal Trail Trail { get; }

        public string id_ { get; }
        public double get_x() => Trail.mTrailCenter.X;
        public double get_y() => Trail.mTrailCenter.Y;
        public bool is_dead() => Trail is null || Trail.mDead;

        public ShowcaseTrail set_position(double x, double y)
        {
            Vector2 previousCenter = Trail.mTrailCenter;
            Vector2 nextCenter = new((float)x, (float)y);
            Trail.mTrailCenter = nextCenter;
            TranslateExistingPoints(previousCenter - nextCenter);
            return this;
        }

        public ShowcaseTrail add_point(double x, double y)
        {
            _manualPoints = true;
            Trail.AddPoint((float)x, (float)y);
            return this;
        }

        public ShowcaseTrail set_color(double red, double green, double blue)
        {
            return set_color(red, green, blue, 255);
        }

        public ShowcaseTrail set_color(double red, double green, double blue, double alpha)
        {
            Trail.mColorOverride = new SexyColor(
                ClampColor(red),
                ClampColor(green),
                ClampColor(blue),
                ClampColor(alpha));
            return this;
        }

        public ShowcaseTrail die()
        {
            Trail.mDead = true;
            return this;
        }

        internal void PrepareForAttachment()
        {
            _attached = true;
            _manualPoints = true;
            Vector2 previousCenter = Trail.mTrailCenter;
            Trail.mTrailCenter = Vector2.Zero;
            TranslateExistingPoints(previousCenter);
        }

        internal void UpdateAttachedPath()
        {
            if (!_attached || Trail is null || Trail.mDead)
            {
                return;
            }

            Vector2 worldPosition = Trail.mTrailCenter;
            Trail.mTrailCenter = Vector2.Zero;
            Trail.AddPoint(worldPosition.X, worldPosition.Y);
        }

        internal void UpdateStandalonePath()
        {
            if (_attached || _manualPoints || Trail is null || Trail.mDead)
            {
                return;
            }

            _tick++;
            float t = (_tick % 220) / 219f;
            float x = _baseX + 120f + t * 600f;
            float y = _baseY + 280f + System.MathF.Sin((t * 2.5f + _tick * 0.002f) * System.MathF.PI * 2f) * 92f;
            Trail.AddPoint(x, y);
        }

        private void TranslateExistingPoints(Vector2 delta)
        {
            if (Trail is null || Trail.mNumTrailPoints == 0 || delta.LengthSquared() <= 0.0001f)
            {
                return;
            }

            for (int i = 0; i < Trail.mNumTrailPoints; i++)
            {
                Trail.mTrailPoints[i].aPos += delta;
            }
        }

        private static int ClampColor(double value)
        {
            return System.Math.Clamp((int)System.Math.Round(value), 0, 255);
        }
    }
}
