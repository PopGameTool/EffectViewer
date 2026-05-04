namespace EffectViewer.Runtime.Showcase
{
    public sealed class ShowcaseVector3
    {
        public ShowcaseVector3(double x, double y, double z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }

        public double x { get; set; }
        public double y { get; set; }
        public double z { get; set; }
        public double length => System.Math.Sqrt(x * x + y * y + z * z);
        public double length_squared => x * x + y * y + z * z;

        public ShowcaseVector3 set(double x, double y, double z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
            return this;
        }

        public ShowcaseVector3 copy_from(ShowcaseVector3 other)
        {
            if (other is not null)
            {
                x = other.x;
                y = other.y;
                z = other.z;
            }

            return this;
        }

        public ShowcaseVector3 clone()
        {
            return new ShowcaseVector3(x, y, z);
        }

        public ShowcaseVector3 add(double otherX, double otherY, double otherZ)
        {
            x += otherX;
            y += otherY;
            z += otherZ;
            return this;
        }

        public ShowcaseVector3 subtract(double otherX, double otherY, double otherZ)
        {
            x -= otherX;
            y -= otherY;
            z -= otherZ;
            return this;
        }

        public ShowcaseVector3 scale(double value)
        {
            x *= value;
            y *= value;
            z *= value;
            return this;
        }

        public double dot(double otherX, double otherY, double otherZ)
        {
            return x * otherX + y * otherY + z * otherZ;
        }

        public ShowcaseVector3 normalize_safe()
        {
            double magnitude = length;
            if (magnitude <= 0.000001d)
            {
                x = 0;
                y = 0;
                z = 0;
            }
            else
            {
                x /= magnitude;
                y /= magnitude;
                z /= magnitude;
            }

            return this;
        }
    }
}
