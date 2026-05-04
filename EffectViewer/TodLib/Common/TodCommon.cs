using System;
using System.Runtime.CompilerServices;
using InlineArray3TriVertex = System.Runtime.CompilerServices.InlineArray3<EffectViewer.TodLib.Common.TriVertex>;

namespace EffectViewer.TodLib.Common
{
    public static class TodCommon
    {
        public const float DEG_TO_RAD = 0.017453292f;

        public const float RAD_TO_DEG = 57.29578f;

        public static float FloatLerp(float theStart, float theEnd, float theFactor)
        {
            return theStart + theFactor * (theEnd - theStart);
        }

        public static int ClampInt(int theNum, int theMin, int theMax)
        {
            if (theNum <= theMin)
            {
                return theMin;
            }

            if (theNum >= theMax)
            {
                return theMax;
            }

            return theNum;
        }

        public static float ClampFloat(float theNum, float theMin, float theMax)
        {
            if (theNum <= theMin)
            {
                return theMin;
            }

            if (theNum >= theMax)
            {
                return theMax;
            }

            return theNum;
        }

        public static int FloatRoundToInt(float theFloatValue)
        {
            if (theFloatValue > 0f)
            {
                return (int)(theFloatValue + 0.5f);
            }

            return (int)(theFloatValue - 0.5f);
        }

        public static bool FloatApproxEqual(float theFloatVal1, float theFloatVal2)
        {
            return MathF.Abs(theFloatVal1 - theFloatVal2) < 1E-06f;
        }

        public static float Distance2D(float x1, float y1, float x2, float y2)
        {
            return MathF.Sqrt((x2 - x1) * (x2 - x1) + (y2 - y1) * (y2 - y1));
        }

        public static float DegToRad(float theAngle)
        {
            return theAngle * DEG_TO_RAD;
        }

        public static float RadToDeg(float theAngle)
        {
            return theAngle * RAD_TO_DEG;
        }

        public static void SetBit(ref uint theNum, int theIdx, int theValue)
        {
            if (theValue != 0)
            {
                theNum |= 1U << theIdx;
                return;
            }

            theNum &= ~(1U << theIdx);
        }

        public static bool TestBit(uint theNum, int theIdx)
        {
            return (theNum & (1U << theIdx)) != 0U;
        }

        public static float TodCurveS(float theTime)
        {
            return 3f * theTime * theTime - 2f * theTime * theTime * theTime;
        }

        public static float TodCurveEvaluate(float theTime, float thePositionStart, float thePositionEnd, TodCurves theCurve)
        {
            float aWarpedTime;
            switch (theCurve)
            {
            case TodCurves.Constant:
                aWarpedTime = 0f;
                break;
            case TodCurves.Linear:
                aWarpedTime = theTime;
                break;
            case TodCurves.EaseIn:
                aWarpedTime = TodCurveQuad(theTime);
                break;
            case TodCurves.EaseOut:
                aWarpedTime = TodCurveInvQuad(theTime);
                break;
            case TodCurves.EaseInOut:
                aWarpedTime = TodCurveS(TodCurveS(theTime));
                break;
            case TodCurves.EaseInOutWeak:
                aWarpedTime = TodCurveS(theTime);
                break;
            case TodCurves.FastInOut:
                aWarpedTime = TodCurveInvQuadS(TodCurveInvQuadS(theTime));
                break;
            case TodCurves.FastInOutWeak:
                aWarpedTime = TodCurveInvQuadS(theTime);
                break;
            case TodCurves.Bounce:
                aWarpedTime = TodCurveBounce(theTime);
                break;
            case TodCurves.BounceFastMiddle:
                aWarpedTime = TodCurveQuad(TodCurveBounce(theTime));
                break;
            case TodCurves.BounceSlowMiddle:
                aWarpedTime = TodCurveInvQuad(TodCurveBounce(theTime));
                break;
            case TodCurves.SinWave:
                aWarpedTime = MathF.Sin(theTime * MathF.Tau);
                break;
            case TodCurves.EaseSinWave:
                aWarpedTime = MathF.Sin(TodCurveS(theTime) * MathF.Tau);
                break;
            default:
                Debug.ASSERT(false);
                aWarpedTime = 0f;
                break;
            }

            return thePositionStart + (thePositionEnd - thePositionStart) * aWarpedTime;
        }

        public static float TodAnimateCurveFloatTime(float theTimeStart, float theTimeEnd, float theTimeAge, float thePositionStart, float thePositionEnd, TodCurves theCurve)
        {
            float aWarpedAge = (theTimeAge - theTimeStart) / (theTimeEnd - theTimeStart);
            return TodCurveEvaluateClamped(aWarpedAge, thePositionStart, thePositionEnd, theCurve);
        }

        public static float TodAnimateCurveFloat(int theTimeStart, int theTimeEnd, int theTimeAge, float thePositionStart, float thePositionEnd, TodCurves theCurve)
        {
            float aWarpedAge = (theTimeAge - theTimeStart) / (float)(theTimeEnd - theTimeStart);
            return TodCurveEvaluateClamped(aWarpedAge, thePositionStart, thePositionEnd, theCurve);
        }

        public static int TodAnimateCurve(int theTimeStart, int theTimeEnd, int theTimeAge, int thePositionStart, int thePositionEnd, TodCurves theCurve)
        {
            return FloatRoundToInt(TodAnimateCurveFloat(theTimeStart, theTimeEnd, theTimeAge, thePositionStart, thePositionEnd, theCurve));
        }

        public static float RandRangeFloat(float theMin, float theMax)
        {
            if (theMin >= theMax)
            {
                return theMin;
            }
            return theMin + RandomNumbers.Rand(theMax - theMin);
        }

        public static int RandRangeInt(int theMin, int theMax)
        {
            if (theMin >= theMax)
            {
                return theMin;
            }
            return theMin + RandomNumbers.Rand(theMax - theMin + 1);
        }

        public static void TodDrawStringMatrix(EffectViewer.TodLib.Graphics.Graphics g, Font theFont, in Matrix4x4 theMatrix, string theString, in SexyColor theColor)
        {

        }

        public static void TodBltMatrix(EffectViewer.TodLib.Graphics.Graphics g, Image theImage, in Matrix4x4 theTransform, in Rectangle theClipRect, in SexyColor theColor, DrawMode theDrawMode, in Rectangle theSrcRect)
        {
            if (theImage == null || theSrcRect.Width <= 0 || theSrcRect.Height <= 0)
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

            DrawMode oldMode = g.mDrawMode;
            g.mDrawMode = theDrawMode;
            InlineArray3TriVertex first = new();
            first[0] = BuildTriVertex(topLeft, u0, v0, theColor);
            first[1] = BuildTriVertex(topRight, u1, v0, theColor);
            first[2] = BuildTriVertex(bottomRight, u1, v1, theColor);

            InlineArray3TriVertex second = new();
            second[0] = BuildTriVertex(topLeft, u0, v0, theColor);
            second[1] = BuildTriVertex(bottomRight, u1, v1, theColor);
            second[2] = BuildTriVertex(bottomLeft, u0, v1, theColor);

            InlineArray2<InlineArray3TriVertex> triangles = new();
            triangles[0] = first;
            triangles[1] = second;
            g.DrawTrianglesTex(theImage, triangles);
            g.mDrawMode = oldMode;
        }

        public static void SexyMatrix3Translation(ref Matrix4x4 m, float x, float y)
        {
            m.Translation += new Vector3(x, y, 0f);
        }

        public static void SexyMatrix3Multiply(ref Matrix4x4 m, Matrix4x4 l, Matrix4x4 r)
        {
            m = r * l;
        }

        private static TriVertex BuildTriVertex(Vector2 position, float u, float v, in SexyColor color)
        {
            return new TriVertex
            {
                Position = new Vector3(position, 0f),
                TextureCoordinate = new Vector2(u, v),
                Color = color
            };
        }

        public static void TodScaleTransformMatrix(out Matrix4x4 m, float x, float y, float theScaleX, float theScaleY)
        {
            m = Matrix4x4.CreateScale(new Vector3(theScaleX, theScaleY, 1f)) * Matrix4x4.CreateTranslation(new Vector3(x, y, 0f));
        }

        public static void TodScaleRotateTransformMatrix(ref Matrix4x4 m, float x, float y, float rad, float theScaleX, float theScaleY)
        {
            m = Matrix4x4.CreateScale(new Vector3(theScaleX, theScaleY, 1f)) * Matrix4x4.CreateRotationZ(-rad) * Matrix4x4.CreateTranslation(new Vector3(x, y, 0f));
        }

        public static SexyColor GetFlashingColor(int theCounter, int theFlashTime)
        {
            int aTimeAge = theCounter % theFlashTime;
            int aTimeInf = theFlashTime / 2;
            int aGrayness = ClampInt(55 + Math.Abs(aTimeInf - aTimeAge) * 200 / aTimeInf, 0, 255);
            return new SexyColor(aGrayness, aGrayness, aGrayness, 255);
        }

        public static int ColorComponentMultiply(int theColor1, int theColor2)
        {
            return ClampInt(theColor1 * theColor2 / 255, 0, 255);
        }

        public static float TodCurveQuad(float theTime)
        {
            return theTime * theTime;
        }

        public static float TodCurveInvQuad(float theTime)
        {
            return 2f * theTime - theTime * theTime;
        }

        public static float TodCurveQuadS(float theTime)
        {
            if (theTime <= 0.5f)
            {
                return TodCurveQuad(theTime * 2f) * 0.5f;
            }

            return TodCurveInvQuad((theTime - 0.5f) * 2f) * 0.5f + 0.5f;
        }

        public static float TodCurveInvQuadS(float theTime)
        {
            if (theTime <= 0.5f)
            {
                return TodCurveInvQuad(theTime * 2f) * 0.5f;
            }

            return TodCurveQuad((theTime - 0.5f) * 2f) * 0.5f + 0.5f;
        }

        public static float TodCurveCubic(float theTime)
        {
            return theTime * theTime * theTime;
        }

        public static float TodCurveInvCubic(float theTime)
        {
            return (theTime - 1f) * (theTime - 1f) * (theTime - 1f) + 1f;
        }

        public static float TodCurveCubicS(float theTime)
        {
            if (theTime <= 0.5f)
            {
                return TodCurveCubic(theTime * 2f) * 0.5f;
            }

            return TodCurveInvCubic((theTime - 0.5f) * 2f) * 0.5f + 0.5f;
        }

        public static float TodCurvePoly(float theTime, float thePoly)
        {
            return MathF.Pow(theTime, thePoly);
        }

        public static float TodCurveInvPoly(float theTime, float thePoly)
        {
            return MathF.Pow(theTime - 1f, thePoly) + 1f;
        }

        public static float TodCurvePolyS(float theTime, float thePoly)
        {
            if (theTime <= 0.5f)
            {
                return TodCurvePoly(theTime * 2f, thePoly) * 0.5f;
            }

            return TodCurveInvPoly((theTime - 0.5f) * 2f, thePoly) * 0.5f + 0.5f;
        }

        public static float TodCurveCircle(float theTime)
        {
            if (theTime > 1 - 1E-06f)
            {
                return 1f;
            }

            return 1f - MathF.Sqrt(1f - theTime * theTime);
        }

        public static float TodCurveInvCircle(float theTime)
        {
            if (theTime < 1E-06f)
            {
                return 0f;
            }

            return MathF.Sqrt(1f - (theTime - 1f) * (theTime - 1f));
        }

        public static float TodCurveBounce(float theTime)
        {
            return 1f - MathF.Abs(1f - theTime * 2f);
        }

        public static float TodCurveEvaluateClamped(float theTime, float thePositionStart, float thePositionEnd, TodCurves theCurve)
        {
            if (theTime <= 0f)
            {
                return thePositionStart;
            }

            if (theTime < 1f)
            {
                return TodCurveEvaluate(theTime, thePositionStart, thePositionEnd, theCurve);
            }

            if (theCurve == TodCurves.Bounce ||
                theCurve == TodCurves.BounceSlowMiddle ||
                theCurve == TodCurves.BounceFastMiddle ||
                theCurve == TodCurves.SinWave ||
                theCurve == TodCurves.EaseSinWave)
            {
                return thePositionStart;
            }

            return thePositionEnd;
        }

        public static void SexyMatrix3Transpose(in Matrix4x4 m, out Matrix4x4 r)
        {
            r = Matrix4x4.Transpose(m);
        }

        public static void SexyMatrix3Inverse(in Matrix4x4 mat, out Matrix4x4 r)
        {
            if (!Matrix4x4.Invert(mat, out r))
            {
                r = Matrix4x4.Identity;
            }
        }

        public static void SexyMatrix3ExtractScale(in Matrix4x4 m, out float theScaleX, out float theScaleY)
        {
            float kx = MathF.Atan2(m.M11, m.M21);
            if (MathF.Abs(kx) < 0.7853982f || MathF.Abs(kx) > 2.3561945f)
            {
                theScaleX = m.M21 / MathF.Cos(kx);
            }
            else
            {
                theScaleX = m.M11 / MathF.Sin(kx);
            }

            float ky = MathF.Atan2(m.M22, m.M12);
            if (MathF.Abs(ky) < 0.7853982f || MathF.Abs(ky) > 2.3561945f)
            {
                theScaleY = m.M12 / MathF.Cos(ky);
            }
            else
            {
                theScaleY = m.M22 / MathF.Sin(ky);
            }
        }

        public static SexyColor ColorAdd(in SexyColor theColor1, in SexyColor theColor2)
        {
            int r = theColor1.mRed + theColor2.mRed;
            int g = theColor1.mGreen + theColor2.mGreen;
            int b = theColor1.mBlue + theColor2.mBlue;
            int a = theColor1.mAlpha + theColor2.mAlpha;
            return new(ClampInt(r, 0, 255), ClampInt(g, 0, 255), ClampInt(b, 0, 255), ClampInt(a, 0, 255));
        }

        public static SexyColor ColorsMultiply(in SexyColor theColor1, in SexyColor theColor2)
        {
            return new SexyColor
            {
                mRed = ColorComponentMultiply(theColor1.mRed, theColor2.mRed),
                mGreen = ColorComponentMultiply(theColor1.mGreen, theColor2.mGreen),
                mBlue = ColorComponentMultiply(theColor1.mBlue, theColor2.mBlue),
                mAlpha = ColorComponentMultiply(theColor1.mAlpha, theColor2.mAlpha)
            };
        }
    }
}
