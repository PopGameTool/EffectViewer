namespace EffectViewer.Runtime.Showcase
{
    public sealed class ShowcaseMatrix
    {
        public ShowcaseMatrix(double m11, double m12, double m21, double m22, double x, double y)
        {
            this.m11 = m11;
            this.m12 = m12;
            this.m21 = m21;
            this.m22 = m22;
            this.x = x;
            this.y = y;
        }

        internal ShowcaseMatrix(Matrix4x4 matrix)
        {
            m11 = matrix.M11;
            m12 = matrix.M12;
            m21 = matrix.M21;
            m22 = matrix.M22;
            x = matrix.M41;
            y = matrix.M42;
        }

        public double m11 { get; }
        public double m12 { get; }
        public double m21 { get; }
        public double m22 { get; }
        public double x { get; }
        public double y { get; }

        public double transform_x(double pointX, double pointY)
        {
            return pointX * m11 + pointY * m21 + x;
        }

        public double transform_y(double pointX, double pointY)
        {
            return pointX * m12 + pointY * m22 + y;
        }

        public ShowcaseVector transform(double pointX, double pointY)
        {
            return new ShowcaseVector(transform_x(pointX, pointY), transform_y(pointX, pointY));
        }

        public ShowcaseMatrix translate(double offsetX, double offsetY)
        {
            return new ShowcaseMatrix(m11, m12, m21, m22, x + offsetX, y + offsetY);
        }

        public ShowcaseMatrix scale(double scale)
        {
            return this.scale(scale, scale);
        }

        public ShowcaseMatrix scale(double scaleX, double scaleY)
        {
            return new ShowcaseMatrix(m11 * scaleX, m12 * scaleX, m21 * scaleY, m22 * scaleY, x, y);
        }
    }
}
