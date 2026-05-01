using EffectViewer.TodLib.Trail;

namespace EffectViewer.Runtime.Showcase
{
    public sealed class ShowcaseTrailPoint
    {
        private readonly Trail _trail;
        private readonly int _index;

        internal ShowcaseTrailPoint(Trail trail, int index)
        {
            _trail = trail;
            _index = index;
        }

        public int index => _index;

        public double x
        {
            get => IsValid ? _trail.mTrailPoints[_index].aPos.X : 0;
            set
            {
                if (IsValid)
                {
                    _trail.mTrailPoints[_index].aPos.X = (float)value;
                }
            }
        }

        public double y
        {
            get => IsValid ? _trail.mTrailPoints[_index].aPos.Y : 0;
            set
            {
                if (IsValid)
                {
                    _trail.mTrailPoints[_index].aPos.Y = (float)value;
                }
            }
        }

        public ShowcaseTrailPoint set_position(double x, double y)
        {
            if (IsValid)
            {
                _trail.mTrailPoints[_index].aPos.X = (float)x;
                _trail.mTrailPoints[_index].aPos.Y = (float)y;
            }

            return this;
        }

        private bool IsValid => _trail is not null && _index >= 0 && _index < _trail.mNumTrailPoints;
    }
}
