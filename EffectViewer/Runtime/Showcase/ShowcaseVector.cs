namespace EffectViewer.Runtime.Showcase
{
    public sealed class ShowcaseVector
    {
        public ShowcaseVector(double x, double y)
        {
            this.x = x;
            this.y = y;
        }

        public double x { get; }
        public double y { get; }
        public double length => System.Math.Sqrt(x * x + y * y);
        public double length_squared => x * x + y * y;

        public ShowcaseVector add(double otherX, double otherY)
        {
            return new ShowcaseVector(x + otherX, y + otherY);
        }

        public ShowcaseVector subtract(double otherX, double otherY)
        {
            return new ShowcaseVector(x - otherX, y - otherY);
        }

        public ShowcaseVector scale(double value)
        {
            return new ShowcaseVector(x * value, y * value);
        }

        public ShowcaseVector normalize()
        {
            double magnitude = length;
            return magnitude <= 0.000001d
                ? new ShowcaseVector(0, 0)
                : new ShowcaseVector(x / magnitude, y / magnitude);
        }

        public double dot(double otherX, double otherY)
        {
            return x * otherX + y * otherY;
        }
    }
}
