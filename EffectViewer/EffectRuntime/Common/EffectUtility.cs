using System;
using System.Collections.Generic;

namespace EffectViewer.EffectRuntime.Common
{
    public static class EffectUtility
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

        public static float CurveS(float theTime)
        {
            return 3f * theTime * theTime - 2f * theTime * theTime * theTime;
        }

        public static float EvaluateCurve(float theTime, float thePositionStart, float thePositionEnd, CurveType theCurve)
        {
            float aWarpedTime;
            switch (theCurve)
            {
            case CurveType.Constant:
                aWarpedTime = 0f;
                break;
            case CurveType.Linear:
                aWarpedTime = theTime;
                break;
            case CurveType.EaseIn:
                aWarpedTime = CurveQuad(theTime);
                break;
            case CurveType.EaseOut:
                aWarpedTime = CurveInvQuad(theTime);
                break;
            case CurveType.EaseInOut:
                aWarpedTime = CurveS(CurveS(theTime));
                break;
            case CurveType.EaseInOutWeak:
                aWarpedTime = CurveS(theTime);
                break;
            case CurveType.FastInOut:
                aWarpedTime = CurveInvQuadS(CurveInvQuadS(theTime));
                break;
            case CurveType.FastInOutWeak:
                aWarpedTime = CurveInvQuadS(theTime);
                break;
            case CurveType.Bounce:
                aWarpedTime = CurveBounce(theTime);
                break;
            case CurveType.BounceFastMiddle:
                aWarpedTime = CurveQuad(CurveBounce(theTime));
                break;
            case CurveType.BounceSlowMiddle:
                aWarpedTime = CurveInvQuad(CurveBounce(theTime));
                break;
            case CurveType.SinWave:
                aWarpedTime = MathF.Sin(theTime * MathF.Tau);
                break;
            case CurveType.EaseSinWave:
                aWarpedTime = MathF.Sin(CurveS(theTime) * MathF.Tau);
                break;
            default:
                Debug.Assert(false);
                aWarpedTime = 0f;
                break;
            }

            return thePositionStart + (thePositionEnd - thePositionStart) * aWarpedTime;
        }

        public static float AnimateCurveFloatTime(float theTimeStart, float theTimeEnd, float theTimeAge, float thePositionStart, float thePositionEnd, CurveType theCurve)
        {
            float aWarpedAge = (theTimeAge - theTimeStart) / (theTimeEnd - theTimeStart);
            return EvaluateCurveClamped(aWarpedAge, thePositionStart, thePositionEnd, theCurve);
        }

        public static float AnimateCurveFloat(int theTimeStart, int theTimeEnd, int theTimeAge, float thePositionStart, float thePositionEnd, CurveType theCurve)
        {
            float aWarpedAge = (theTimeAge - theTimeStart) / (float)(theTimeEnd - theTimeStart);
            return EvaluateCurveClamped(aWarpedAge, thePositionStart, thePositionEnd, theCurve);
        }

        public static int AnimateCurve(int theTimeStart, int theTimeEnd, int theTimeAge, int thePositionStart, int thePositionEnd, CurveType theCurve)
        {
            return FloatRoundToInt(AnimateCurveFloat(theTimeStart, theTimeEnd, theTimeAge, thePositionStart, thePositionEnd, theCurve));
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

        public static void DrawStringMatrix(EffectViewer.EffectRuntime.Graphics.Graphics g, Font theFont, in Matrix4x4 theMatrix, string theString, in EffectColor theColor)
        {
            if (g == null || theFont == null || !theFont.IsInitialized || string.IsNullOrEmpty(theString))
            {
                return;
            }

            if (theFont.IsTrueType)
            {
                DrawTrueTypeStringMatrix(g, theFont, theMatrix, theString, theColor);
                return;
            }

            InlineArray256<List<FontRenderCommand>> renderBucketMemory = new();
            Span<List<FontRenderCommand>> renderBuckets = renderBucketMemory;
            int curXPos = 0;
            for (int charIndex = 0; charIndex < theString.Length; charIndex++)
            {
                char sourceChar = theString[charIndex];
                char mappedChar = theFont.GetMappedChar(sourceChar);
                char nextChar = charIndex < theString.Length - 1
                    ? theFont.GetMappedChar(theString[charIndex + 1])
                    : '\0';
                int maxXPos = curXPos;
                foreach (FontLayer layer in theFont.Layers)
                {
                    FontCharData charData = layer.GetCharData(mappedChar);
                    Rectangle srcRect = charData.ImageRect;
                    float scale = theFont.mScale;
                    int layerPointSize = layer.PointSize;
                    if (layerPointSize != 0)
                    {
                        scale *= theFont.mPointSize / (float)layerPointSize;
                    }

                    int imageX;
                    int imageY;
                    int charWidth;
                    int spacing;
                    if (FloatApproxEqual(scale, 1f))
                    {
                        imageX = charData.Offset.X + layer.Offset.X + curXPos;
                        imageY = charData.Offset.Y + layer.Offset.Y - layer.Ascent;
                        charWidth = charData.Width;
                        spacing = nextChar == '\0' ? 0 : layer.Spacing + charData.GetKerning(nextChar);
                    }
                    else
                    {
                        imageX = curXPos + (int)MathF.Floor((charData.Offset.X + layer.Offset.X) * scale);
                        imageY = -(int)MathF.Floor((layer.Ascent - layer.Offset.Y - charData.Offset.Y) * scale);
                        charWidth = (int)(charData.Width * scale);
                        spacing = nextChar == '\0' ? 0 : (int)((layer.Spacing + charData.GetKerning(nextChar)) * scale);
                    }

                    if (srcRect.Width <= 0 || srcRect.Height <= 0)
                    {
                        maxXPos = Math.Max(maxXPos, curXPos + spacing + charWidth);
                        continue;
                    }

                    EffectColor color = new(
                        Math.Min(layer.ColorAdd.mRed + theColor.mRed * layer.ColorMult.mRed / 255, 255),
                        Math.Min(layer.ColorAdd.mGreen + theColor.mGreen * layer.ColorMult.mGreen / 255, 255),
                        Math.Min(layer.ColorAdd.mBlue + theColor.mBlue * layer.ColorMult.mBlue / 255, 255),
                        Math.Min(layer.ColorAdd.mAlpha + theColor.mAlpha * layer.ColorMult.mAlpha / 255, 255));

                    int orderIndex = ClampInt(charData.Order + layer.BaseOrder + 128, 0, 255);
                    renderBuckets[orderIndex] ??= [];
                    renderBuckets[orderIndex].Add(new FontRenderCommand(layer.Image, imageX, imageY, srcRect, layer.DrawMode, color));
                    maxXPos = Math.Max(maxXPos, curXPos + spacing + charWidth);
                }

                curXPos = maxXPos;
            }

            DrawMode oldDrawMode = g.GetDrawMode();
            foreach (List<FontRenderCommand> bucket in renderBuckets)
            {
                if (bucket is null)
                {
                    continue;
                }

                foreach (FontRenderCommand command in bucket)
                {
                    if (command.Image == null)
                    {
                        continue;
                    }

                    Matrix4x4 glyphMatrix = Matrix4x4.Identity;
                    Matrix3Translation(ref glyphMatrix, command.SourceRect.Width * 0.5f + command.X, command.SourceRect.Height * 0.5f + command.Y);
                    Matrix3Multiply(ref glyphMatrix, theMatrix, glyphMatrix);

                    DrawMode drawMode = command.DrawMode >= 0 ? (DrawMode)command.DrawMode : oldDrawMode;
                    BlitMatrix(g, command.Image, glyphMatrix, g.mClipRect, command.Color, drawMode, command.SourceRect);
                }
            }
        }

        private static void DrawTrueTypeStringMatrix(EffectViewer.EffectRuntime.Graphics.Graphics g, Font theFont, in Matrix4x4 theMatrix, string theString, in EffectColor theColor)
        {
            theFont.PrepareTrueTypeText(theString);
            Image image = theFont.TrueTypeAtlas?.TextureImage;
            if (image == null)
            {
                return;
            }

            DrawMode oldDrawMode = g.GetDrawMode();
            int curXPos = 0;
            foreach (char c in theString)
            {
                if (char.IsControl(c))
                {
                    continue;
                }

                if (!theFont.TryGetTrueTypeGlyph(c, out TrueTypeGlyphData glyph))
                {
                    curXPos += theFont.CharWidth(c);
                    continue;
                }

                Matrix4x4 glyphMatrix = Matrix4x4.Identity;
                Matrix3Translation(
                    ref glyphMatrix,
                    glyph.SourceRect.Width * 0.5f + curXPos + glyph.Left,
                    glyph.SourceRect.Height * 0.5f + glyph.Top);
                Matrix3Multiply(ref glyphMatrix, theMatrix, glyphMatrix);
                BlitMatrix(g, image, glyphMatrix, g.mClipRect, theColor, oldDrawMode, glyph.SourceRect);
                curXPos += glyph.Advance;
            }
        }

        public static void BlitMatrix(EffectViewer.EffectRuntime.Graphics.Graphics g, Image theImage, in Matrix4x4 theTransform, in Rectangle theClipRect, in EffectColor theColor, DrawMode theDrawMode, in Rectangle theSrcRect)
        {
            g?.DrawImageMatrix(theImage, theTransform, theClipRect, theColor, theDrawMode, theSrcRect);
        }

        public static void Matrix3Translation(ref Matrix4x4 m, float x, float y)
        {
            m.Translation += new Vector3(x, y, 0f);
        }

        public static void Matrix3Multiply(ref Matrix4x4 m, Matrix4x4 l, Matrix4x4 r)
        {
            m = r * l;
        }

        private readonly record struct FontRenderCommand(
            Image Image,
            int X,
            int Y,
            Rectangle SourceRect,
            int DrawMode,
            EffectColor Color);

        public static void ScaleTransformMatrix(out Matrix4x4 m, float x, float y, float theScaleX, float theScaleY)
        {
            m = Matrix4x4.CreateScale(new Vector3(theScaleX, theScaleY, 1f)) * Matrix4x4.CreateTranslation(new Vector3(x, y, 0f));
        }

        public static void ScaleRotateTransformMatrix(ref Matrix4x4 m, float x, float y, float rad, float theScaleX, float theScaleY)
        {
            m = Matrix4x4.CreateScale(new Vector3(theScaleX, theScaleY, 1f)) * Matrix4x4.CreateRotationZ(-rad) * Matrix4x4.CreateTranslation(new Vector3(x, y, 0f));
        }

        public static EffectColor GetFlashingColor(int theCounter, int theFlashTime)
        {
            int aTimeAge = theCounter % theFlashTime;
            int aTimeInf = theFlashTime / 2;
            int aGrayness = ClampInt(55 + Math.Abs(aTimeInf - aTimeAge) * 200 / aTimeInf, 0, 255);
            return new EffectColor(aGrayness, aGrayness, aGrayness, 255);
        }

        public static int ColorComponentMultiply(int theColor1, int theColor2)
        {
            return ClampInt(theColor1 * theColor2 / 255, 0, 255);
        }

        public static float CurveQuad(float theTime)
        {
            return theTime * theTime;
        }

        public static float CurveInvQuad(float theTime)
        {
            return 2f * theTime - theTime * theTime;
        }

        public static float CurveQuadS(float theTime)
        {
            if (theTime <= 0.5f)
            {
                return CurveQuad(theTime * 2f) * 0.5f;
            }

            return CurveInvQuad((theTime - 0.5f) * 2f) * 0.5f + 0.5f;
        }

        public static float CurveInvQuadS(float theTime)
        {
            if (theTime <= 0.5f)
            {
                return CurveInvQuad(theTime * 2f) * 0.5f;
            }

            return CurveQuad((theTime - 0.5f) * 2f) * 0.5f + 0.5f;
        }

        public static float CurveCubic(float theTime)
        {
            return theTime * theTime * theTime;
        }

        public static float CurveInvCubic(float theTime)
        {
            return (theTime - 1f) * (theTime - 1f) * (theTime - 1f) + 1f;
        }

        public static float CurveCubicS(float theTime)
        {
            if (theTime <= 0.5f)
            {
                return CurveCubic(theTime * 2f) * 0.5f;
            }

            return CurveInvCubic((theTime - 0.5f) * 2f) * 0.5f + 0.5f;
        }

        public static float CurvePoly(float theTime, float thePoly)
        {
            return MathF.Pow(theTime, thePoly);
        }

        public static float CurveInvPoly(float theTime, float thePoly)
        {
            return MathF.Pow(theTime - 1f, thePoly) + 1f;
        }

        public static float CurvePolyS(float theTime, float thePoly)
        {
            if (theTime <= 0.5f)
            {
                return CurvePoly(theTime * 2f, thePoly) * 0.5f;
            }

            return CurveInvPoly((theTime - 0.5f) * 2f, thePoly) * 0.5f + 0.5f;
        }

        public static float CurveCircle(float theTime)
        {
            if (theTime > 1 - 1E-06f)
            {
                return 1f;
            }

            return 1f - MathF.Sqrt(1f - theTime * theTime);
        }

        public static float CurveInvCircle(float theTime)
        {
            if (theTime < 1E-06f)
            {
                return 0f;
            }

            return MathF.Sqrt(1f - (theTime - 1f) * (theTime - 1f));
        }

        public static float CurveBounce(float theTime)
        {
            return 1f - MathF.Abs(1f - theTime * 2f);
        }

        public static float EvaluateCurveClamped(float theTime, float thePositionStart, float thePositionEnd, CurveType theCurve)
        {
            if (theTime <= 0f)
            {
                return thePositionStart;
            }

            if (theTime < 1f)
            {
                return EvaluateCurve(theTime, thePositionStart, thePositionEnd, theCurve);
            }

            if (theCurve == CurveType.Bounce ||
                theCurve == CurveType.BounceSlowMiddle ||
                theCurve == CurveType.BounceFastMiddle ||
                theCurve == CurveType.SinWave ||
                theCurve == CurveType.EaseSinWave)
            {
                return thePositionStart;
            }

            return thePositionEnd;
        }

        public static void Matrix3Transpose(in Matrix4x4 m, out Matrix4x4 r)
        {
            r = Matrix4x4.Transpose(m);
        }

        public static void Matrix3Inverse(in Matrix4x4 mat, out Matrix4x4 r)
        {
            if (!Matrix4x4.Invert(mat, out r))
            {
                r = Matrix4x4.Identity;
            }
        }

        public static void Matrix3ExtractScale(in Matrix4x4 m, out float theScaleX, out float theScaleY)
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

        public static EffectColor ColorAdd(in EffectColor theColor1, in EffectColor theColor2)
        {
            int r = theColor1.mRed + theColor2.mRed;
            int g = theColor1.mGreen + theColor2.mGreen;
            int b = theColor1.mBlue + theColor2.mBlue;
            int a = theColor1.mAlpha + theColor2.mAlpha;
            return new(ClampInt(r, 0, 255), ClampInt(g, 0, 255), ClampInt(b, 0, 255), ClampInt(a, 0, 255));
        }

        public static EffectColor ColorsMultiply(in EffectColor theColor1, in EffectColor theColor2)
        {
            return new EffectColor
            {
                mRed = ColorComponentMultiply(theColor1.mRed, theColor2.mRed),
                mGreen = ColorComponentMultiply(theColor1.mGreen, theColor2.mGreen),
                mBlue = ColorComponentMultiply(theColor1.mBlue, theColor2.mBlue),
                mAlpha = ColorComponentMultiply(theColor1.mAlpha, theColor2.mAlpha)
            };
        }
    }
}
