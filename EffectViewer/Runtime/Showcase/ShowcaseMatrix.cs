using System.Numerics;
using EffectViewer.Runtime.Lua;
using EffectViewer.EffectRuntime.Common;
using MoonSharp.Interpreter;

namespace EffectViewer.Runtime.Showcase
{
    public sealed class ShowcaseMatrix
    {
        public ShowcaseMatrix(double m11, double m12, double m21, double m22, double x, double y)
            : this(m11, m12, x, m21, m22, y, 0, 0, 1)
        {
        }

        public ShowcaseMatrix(
            double m11,
            double m12,
            double m13,
            double m21,
            double m22,
            double m23,
            double m31,
            double m32,
            double m33)
        {
            set(m11, m12, m13, m21, m22, m23, m31, m32, m33);
        }

        internal ShowcaseMatrix(Matrix4x4 matrix)
        {
            FromMatrix4x4(matrix);
        }

        public double m11 { get; set; }
        public double m12 { get; set; }
        public double m13 { get; set; }
        public double m21 { get; set; }
        public double m22 { get; set; }
        public double m23 { get; set; }
        public double m31 { get; set; }
        public double m32 { get; set; }
        public double m33 { get; set; }
        public double x
        {
            get => m13;
            set => m13 = value;
        }

        public double y
        {
            get => m23;
            set => m23 = value;
        }

        public static ShowcaseMatrix Identity()
        {
            return new ShowcaseMatrix(1, 0, 0, 0, 1, 0, 0, 0, 1);
        }

        public ShowcaseMatrix set(
            double m11,
            double m12,
            double m13,
            double m21,
            double m22,
            double m23,
            double m31,
            double m32,
            double m33)
        {
            this.m11 = m11;
            this.m12 = m12;
            this.m13 = m13;
            this.m21 = m21;
            this.m22 = m22;
            this.m23 = m23;
            this.m31 = m31;
            this.m32 = m32;
            this.m33 = m33;
            return this;
        }

        public ShowcaseMatrix set_identity()
        {
            return set(1, 0, 0, 0, 1, 0, 0, 0, 1);
        }

        public ShowcaseMatrix copy_from(ShowcaseMatrix other)
        {
            if (other is not null)
            {
                set(other.m11, other.m12, other.m13, other.m21, other.m22, other.m23, other.m31, other.m32, other.m33);
            }

            return this;
        }

        public ShowcaseMatrix clone()
        {
            return new ShowcaseMatrix(m11, m12, m13, m21, m22, m23, m31, m32, m33);
        }

        public ShowcaseMatrix translation(double x, double y)
        {
            m13 += x;
            m23 += y;
            return this;
        }

        public ShowcaseMatrix translate(double offsetX, double offsetY)
        {
            return clone().translation(offsetX, offsetY);
        }

        public ShowcaseMatrix scale(double value)
        {
            return scale(value, value);
        }

        public ShowcaseMatrix scale(double scaleX, double scaleY)
        {
            return new ShowcaseMatrix(
                m11 * scaleX,
                m12 * scaleY,
                m13,
                m21 * scaleX,
                m22 * scaleY,
                m23,
                m31,
                m32,
                m33);
        }

        public ShowcaseMatrix multiply(ShowcaseMatrix left, ShowcaseMatrix right)
        {
            Matrix4x4 l = left?.ToMatrix4x4() ?? Matrix4x4.Identity;
            Matrix4x4 r = right?.ToMatrix4x4() ?? Matrix4x4.Identity;
            FromMatrix4x4(r * l);
            return this;
        }

        public ShowcaseMatrix transpose(ShowcaseMatrix source)
        {
            EffectUtility.Matrix3Transpose(source?.ToMatrix4x4() ?? Matrix4x4.Identity, out Matrix4x4 result);
            FromMatrix4x4(result);
            return this;
        }

        public ShowcaseMatrix inverse(ShowcaseMatrix source)
        {
            EffectUtility.Matrix3Inverse(source?.ToMatrix4x4() ?? Matrix4x4.Identity, out Matrix4x4 result);
            FromMatrix4x4(result);
            return this;
        }

        public DynValue extract_scale()
        {
            EffectUtility.Matrix3ExtractScale(ToMatrix4x4(), out float scaleX, out float scaleY);
            return DynValue.NewTuple(DynValue.NewNumber(scaleX), DynValue.NewNumber(scaleY));
        }

        public DynValue transform_point(double pointX, double pointY)
        {
            return DynValue.NewTuple(
                DynValue.NewNumber(transform_x(pointX, pointY)),
                DynValue.NewNumber(transform_y(pointX, pointY)));
        }

        public ShowcaseVector transform_vector(ShowcaseVector vector)
        {
            return vector is null ? null : transform(vector.x, vector.y);
        }

        public double transform_x(double pointX, double pointY)
        {
            return pointX * m11 + pointY * m12 + m13;
        }

        public double transform_y(double pointX, double pointY)
        {
            return pointX * m21 + pointY * m22 + m23;
        }

        public ShowcaseVector transform(double pointX, double pointY)
        {
            return new ShowcaseVector(transform_x(pointX, pointY), transform_y(pointX, pointY));
        }

        internal Matrix4x4 ToMatrix4x4()
        {
            return new Matrix4x4(
                (float)m11,
                (float)m21,
                0f,
                (float)m31,
                (float)m12,
                (float)m22,
                0f,
                (float)m32,
                0f,
                0f,
                1f,
                0f,
                (float)m13,
                (float)m23,
                0f,
                (float)m33);
        }

        private void FromMatrix4x4(Matrix4x4 matrix)
        {
            m11 = matrix.M11;
            m12 = matrix.M21;
            m13 = matrix.M41;
            m21 = matrix.M12;
            m22 = matrix.M22;
            m23 = matrix.M42;
            m31 = matrix.M14;
            m32 = matrix.M24;
            m33 = matrix.M44;
        }
    }
}
