using System.Numerics;
using EffectViewer.Runtime.Lua;
using EffectViewer.TodLib.Common;
using EffectViewer.TodLib.Graphics;
using EffectViewer.TodLib.Trail;
using MoonSharp.Interpreter;

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
        public double pos_x
        {
            get => Trail?.mTrailCenter.X ?? 0;
            set
            {
                if (Trail is not null)
                {
                    set_position(value, Trail.mTrailCenter.Y);
                }
            }
        }

        public double pos_y
        {
            get => Trail?.mTrailCenter.Y ?? 0;
            set
            {
                if (Trail is not null)
                {
                    set_position(Trail.mTrailCenter.X, value);
                }
            }
        }

        public bool dead
        {
            get => Trail?.mDead ?? true;
            set
            {
                if (Trail is not null)
                {
                    Trail.mDead = value;
                }
            }
        }

        public int num_trail_points => point_count;

        public int render_order
        {
            get => Trail?.mRenderOrder ?? 0;
            set
            {
                if (Trail is not null)
                {
                    Trail.mRenderOrder = value;
                }
            }
        }

        public int age
        {
            get => Trail?.mTrailAge ?? 0;
            set
            {
                if (Trail is not null)
                {
                    Trail.mTrailAge = System.Math.Max(0, value);
                }
            }
        }

        public int trail_age
        {
            get => age;
            set => age = value;
        }

        public int duration
        {
            get => Trail?.mTrailDuration ?? 0;
            set
            {
                if (Trail is not null)
                {
                    Trail.mTrailDuration = System.Math.Max(1, value);
                }
            }
        }

        public int trail_duration
        {
            get => duration;
            set => duration = value;
        }

        public bool is_attachment
        {
            get => Trail?.mIsAttachment ?? false;
            set
            {
                if (Trail is not null)
                {
                    Trail.mIsAttachment = value;
                }
            }
        }

        public int point_count => Trail?.mNumTrailPoints ?? 0;
        public double trail_center_x
        {
            get => pos_x;
            set => pos_x = value;
        }

        public double trail_center_y
        {
            get => pos_y;
            set => pos_y = value;
        }

        public string image_id => Trail?.mDefinition?.mImage;
        public string image_override_id => Trail?.mImageOverride?.mId;
        public ShowcaseImage image_override
        {
            get => Trail?.mImageOverride is null ? null : new ShowcaseImage(Trail.mImageOverride);
            set
            {
                if (Trail is not null)
                {
                    Trail.mImageOverride = LuaApiUtility.ImageFrom(value);
                }
            }
        }
        public int trail_flags
        {
            get => Trail?.mDefinition?.mTrailFlags ?? 0;
            set
            {
                if (Trail?.mDefinition is not null)
                {
                    Trail.mDefinition.mTrailFlags = value;
                }
            }
        }

        public int max_points
        {
            get => Trail?.mDefinition?.mMaxPoints ?? 0;
            set
            {
                if (Trail?.mDefinition is not null)
                {
                    Trail.mDefinition.mMaxPoints = System.Math.Clamp(value, 2, TodLibConstants.MAX_TRAIL_POINTS);
                }
            }
        }

        public double min_point_distance
        {
            get => Trail?.mDefinition?.mMinPointDistance ?? 0;
            set
            {
                if (Trail?.mDefinition is not null)
                {
                    Trail.mDefinition.mMinPointDistance = (float)System.Math.Max(0, value);
                }
            }
        }

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

        public ShowcaseTrail move(double x, double y)
        {
            return set_position(x, y);
        }

        public ShowcaseTrail offset(double x, double y)
        {
            if (Trail is not null)
            {
                set_position(Trail.mTrailCenter.X + x, Trail.mTrailCenter.Y + y);
            }

            return this;
        }

        public ShowcaseTrail add_point(double x, double y)
        {
            _manualPoints = true;
            Trail.AddPoint((float)x, (float)y);
            return this;
        }

        public ShowcaseTrail clear_points()
        {
            if (Trail is not null)
            {
                Trail.mNumTrailPoints = 0;
                _manualPoints = true;
            }

            return this;
        }

        public ShowcaseTrailPoint point(int index)
        {
            return Trail is null || index < 0 || index >= Trail.mNumTrailPoints
                ? null
                : new ShowcaseTrailPoint(Trail, index);
        }

        public DynValue get_trail_point(int index)
        {
            if (Trail is null || index < 0 || index >= Trail.mNumTrailPoints)
            {
                return DynValue.Nil;
            }

            return DynValue.NewTuple(
                DynValue.NewNumber(Trail.mTrailPoints[index].aPos.X),
                DynValue.NewNumber(Trail.mTrailPoints[index].aPos.Y));
        }

        public ShowcaseTrail set_trail_point(int index, double x, double y)
        {
            if (Trail is not null && index >= 0 && index < Trail.mNumTrailPoints)
            {
                Trail.mTrailPoints[index].aPos.X = (float)x;
                Trail.mTrailPoints[index].aPos.Y = (float)y;
            }

            return this;
        }

        public double get_trail_interp(int index)
        {
            return Trail is not null && index >= 0 && index < (int)TrailTracks.NumTrailTracks
                ? Trail.mTrailInterp[index]
                : 0d;
        }

        public ShowcaseTrail set_trail_interp(int index, double value)
        {
            if (Trail is not null && index >= 0 && index < (int)TrailTracks.NumTrailTracks)
            {
                Trail.mTrailInterp[index] = (float)value;
            }

            return this;
        }

        public ShowcaseVector normal_at(int index)
        {
            if (Trail is null || index < 0 || index >= Trail.mNumTrailPoints)
            {
                return null;
            }

            Vector2 normal = default;
            return Trail.GetNormalAtPoint(index, ref normal) ? new ShowcaseVector(normal.X, normal.Y) : null;
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

        public DynValue get_color_override()
        {
            return Trail is null ? DynValue.Nil : LuaApiUtility.ColorTuple(Trail.mColorOverride);
        }

        public ShowcaseTrail set_color_override(DynValue red, DynValue green, DynValue blue, DynValue alpha)
        {
            if (Trail is not null)
            {
                Trail.mColorOverride = LuaApiUtility.MergeColor(Trail.mColorOverride, red, green, blue, alpha);
            }

            return this;
        }

        public ShowcaseTrail clear_image_override()
        {
            if (Trail is not null)
            {
                Trail.mImageOverride = null;
            }

            return this;
        }

        public bool has_image_override()
        {
            return Trail?.mImageOverride is not null;
        }

        public DynValue get_normal_at_point(int index)
        {
            if (Trail is null || index < 0 || index >= Trail.mNumTrailPoints)
            {
                return DynValue.NewTuple(DynValue.False, DynValue.Nil, DynValue.Nil);
            }

            Vector2 normal = default;
            bool ok = Trail.GetNormalAtPoint(index, ref normal);
            return DynValue.NewTuple(
                DynValue.NewBoolean(ok),
                ok ? DynValue.NewNumber(normal.X) : DynValue.Nil,
                ok ? DynValue.NewNumber(normal.Y) : DynValue.Nil);
        }

        public ShowcaseTrail update()
        {
            Trail?.Update();
            return this;
        }

        public ShowcaseTrail update_attached_path()
        {
            UpdateAttachedPath();
            return this;
        }

        public ShowcaseTrail update_standalone_path()
        {
            UpdateStandalonePath();
            return this;
        }

        public ShowcaseTrail draw(LuaGraphicsApi graphics)
        {
            if (graphics is not null && Trail is { mDead: false })
            {
                Trail.Draw(graphics.Graphics);
            }

            return this;
        }

        public ShowcaseTrail set_manual_points(bool enabled)
        {
            _manualPoints = enabled;
            return this;
        }

        public ShowcaseTrail set_age(double value)
        {
            age = (int)System.Math.Round(value);
            return this;
        }

        public ShowcaseTrail set_duration(double value)
        {
            duration = (int)System.Math.Round(value);
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

        private static Image RequireImage(string imageId)
        {
            Image image = ResourceHandler.GetImage(imageId);
            return image ?? throw new System.InvalidOperationException($"Image '{imageId}' was not found in the current project.");
        }
    }
}
