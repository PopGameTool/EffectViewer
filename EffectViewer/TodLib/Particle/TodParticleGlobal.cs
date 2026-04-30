using System.Collections.Generic;
using System.IO;

namespace EffectViewer.TodLib.Particle
{
    public static class TodParticleGlobal
    {
        public static int gParticleDefCount;
        public static Dictionary<string, TodParticleDefinition> gParticleDefArray;
        public static int gParticleParamArraySize;
        public static ParticleParams[] gParticleParamArray;

        public static void TodParticleLoadDefinitions(ref ParticleParams[] theParticleParamArray,
            int theParticleParamArraySize)
        {
            gParticleParamArraySize = theParticleParamArraySize;
            gParticleParamArray = theParticleParamArray;
            gParticleDefCount = theParticleParamArraySize;
            gParticleDefArray = new Dictionary<string, TodParticleDefinition>();

            for (int i = 0; i < gParticleParamArraySize; i++)
            {
                ParticleParams aParticleParams = gParticleParamArray[i];
                if (!TodParticleLoadADef(out TodParticleDefinition aDef, aParticleParams.mParticleFileName))
                {
                    Debug.Log(DebugType.Error, $"Failed to load particle '{aParticleParams.mParticleFileName}'");
                }
                else
                {
                    gParticleDefArray[aParticleParams.mParticleFileName] = aDef;
                }
            }
        }

        public static void TodParticleFreeDefinitions()
        {
            gParticleDefArray = null;
            gParticleDefCount = 0;
            gParticleParamArray = null;
            gParticleParamArraySize = 0;
        }

        public static bool TodParticleLoadADef(out TodParticleDefinition theParticleDef, string aFilename)
        {
            using (Stream fileStream = File.OpenRead(aFilename))
            {
                theParticleDef = SexyParticleReader.Decode(fileStream);
            }

            for (int i = 0; i < theParticleDef.mEmitterDefCount; i++)
            {
                TodEmitterDefinition aDef = theParticleDef.mEmitterDefs[i];
                Definition.FloatTrackSetDefault(aDef.mSystemDuration, 0f);
                Definition.FloatTrackSetDefault(aDef.mSpawnRate, 0f);
                Definition.FloatTrackSetDefault(aDef.mSpawnMinActive, -1f);
                Definition.FloatTrackSetDefault(aDef.mSpawnMaxActive, -1f);
                Definition.FloatTrackSetDefault(aDef.mSpawnMaxLaunched, -1f);
                Definition.FloatTrackSetDefault(aDef.mEmitterRadius, 0f);
                Definition.FloatTrackSetDefault(aDef.mEmitterOffsetX, 0f);
                Definition.FloatTrackSetDefault(aDef.mEmitterOffsetY, 0f);
                Definition.FloatTrackSetDefault(aDef.mEmitterBoxX, 0f);
                Definition.FloatTrackSetDefault(aDef.mEmitterBoxY, 0f);
                Definition.FloatTrackSetDefault(aDef.mEmitterSkewX, 0f);
                Definition.FloatTrackSetDefault(aDef.mEmitterSkewY, 0f);
                Definition.FloatTrackSetDefault(aDef.mParticleDuration, 100f);
                Definition.FloatTrackSetDefault(aDef.mLaunchSpeed, 0f);
                Definition.FloatTrackSetDefault(aDef.mSystemRed, 1f);
                Definition.FloatTrackSetDefault(aDef.mSystemGreen, 1f);
                Definition.FloatTrackSetDefault(aDef.mSystemBlue, 1f);
                Definition.FloatTrackSetDefault(aDef.mSystemAlpha, 1f);
                Definition.FloatTrackSetDefault(aDef.mSystemBrightness, 1f);
                Definition.FloatTrackSetDefault(aDef.mLaunchAngle, 0f);
                Definition.FloatTrackSetDefault(aDef.mCrossFadeDuration, 0f);
                Definition.FloatTrackSetDefault(aDef.mParticleRed, 1f);
                Definition.FloatTrackSetDefault(aDef.mParticleGreen, 1f);
                Definition.FloatTrackSetDefault(aDef.mParticleBlue, 1f);
                Definition.FloatTrackSetDefault(aDef.mParticleAlpha, 1f);
                Definition.FloatTrackSetDefault(aDef.mParticleBrightness, 1f);
                Definition.FloatTrackSetDefault(aDef.mParticleSpinAngle, 0f);
                Definition.FloatTrackSetDefault(aDef.mParticleSpinSpeed, 0f);
                Definition.FloatTrackSetDefault(aDef.mParticleScale, 1f);
                Definition.FloatTrackSetDefault(aDef.mParticleStretch, 1f);
                Definition.FloatTrackSetDefault(aDef.mCollisionReflect, 0f);
                Definition.FloatTrackSetDefault(aDef.mCollisionSpin, 0f);
                Definition.FloatTrackSetDefault(aDef.mClipTop, 0f);
                Definition.FloatTrackSetDefault(aDef.mClipBottom, 0f);
                Definition.FloatTrackSetDefault(aDef.mClipLeft, 0f);
                Definition.FloatTrackSetDefault(aDef.mClipRight, 0f);
                Definition.FloatTrackSetDefault(aDef.mAnimationRate, 0f);
            }

            return true;
        }

        public static float FloatTrackEvaluateFromLastTime(FloatParameterTrack theTrack, float theTimeValue, float theInterp)
        {
            if (theTimeValue < 0f)
            {
                return 0f;
            }

            return Definition.FloatTrackEvaluate(theTrack, theTimeValue, theInterp);
        }

        public static float CrossFadeLerp(float theFrom, float theTo, bool theFromIsSet, bool theToIsSet, float theFraction)
        {
            if (!theFromIsSet)
            {
                return theTo;
            }

            if (!theToIsSet)
            {
                return theFrom;
            }

            return theFrom + (theTo - theFrom) * theFraction;
        }

        public static void RenderParticle(EffectViewer.TodLib.Graphics.Graphics g, TodParticle theParticle, SexyColor theColor, ref ParticleRenderParams theParams)
        {
            TodParticleEmitter aEmitter = theParticle.mParticleEmitter;
            TodEmitterDefinition aEmitterDef = aEmitter.mEmitterDef;
            Image aImage = aEmitter.mImageOverride ?? ResourceHandler.GetImage(aEmitterDef.mImage);
            if (aImage == null)
            {
                return;  // 不存在贴图时，取消绘制
            }

            int aCelWidth = aImage.GetCelWidth();
            int aCelHeight = aImage.GetCelHeight();
            int aFrame;
            if (aEmitter.mFrameOverride != -1)  // 如果定义了覆写帧
            {
                aFrame = aEmitter.mFrameOverride;
            }
            else if (Definition.FloatTrackIsSet(aEmitterDef.mAnimationRate))  // 如果定义了动画速率
            {
                aFrame = TodCommon.ClampInt((int)(theParticle.mAnimationTimeValue * aEmitterDef.mImageFrames), 0, aEmitterDef.mImageFrames - 1);  // 动画时间值（循环率） * 总帧数得到当前帧
            }
            else if (aEmitterDef.mAnimated != 0)
            {
                aFrame = TodCommon.ClampInt((int)(theParticle.mParticleTimeValue * aEmitterDef.mImageFrames), 0, aEmitterDef.mImageFrames - 1);  // 粒子时间值 * 总帧数得到当前帧
            }
            else
            {
                aFrame = theParticle.mImageFrame;  // 帧固定的粒子，直接取其贴图帧
            }

            aFrame += aEmitterDef.mImageCol;  // 当前帧加上定义中的图像起始列，得到当前帧在贴图中的列数
            if (aFrame >= aImage.mNumCols)
            {
                aFrame = aImage.mNumCols - 1;
            }

            int aRow = aEmitterDef.mImageRow;
            if (aRow >= aImage.mNumRows)
            {
                aRow = aImage.mNumRows - 1;
            }

            Rectangle aSrcRect = new(aCelWidth * aFrame, aCelHeight * aRow, aCelWidth, aCelHeight);
            float aClipTop = TodParticleEmitter.ParticleTrackEvaluate(aEmitterDef.mClipTop, theParticle, ParticleTracks.ParticleClipTop);
            float aClipBottom = TodParticleEmitter.ParticleTrackEvaluate(aEmitterDef.mClipBottom, theParticle, ParticleTracks.ParticleClipBottom);
            float aClipLeft = TodParticleEmitter.ParticleTrackEvaluate(aEmitterDef.mClipLeft, theParticle, ParticleTracks.ParticleClipLeft);
            float aClipRight = TodParticleEmitter.ParticleTrackEvaluate(aEmitterDef.mClipRight, theParticle, ParticleTracks.ParticleClipRight);
            Debug.ASSERT(aClipTop >= 0f && aClipTop <= 1f);
            Debug.ASSERT(aClipBottom >= 0f && aClipBottom <= 1f);
            Debug.ASSERT(aClipLeft >= 0f && aClipLeft <= 1f);
            Debug.ASSERT(aClipRight >= 0f && aClipRight <= 1f);
            theParams.mPosX += aClipLeft * aCelWidth;
            theParams.mPosY += aClipTop * aCelHeight;
            aSrcRect.X += TodCommon.FloatRoundToInt(aClipLeft * aCelWidth);
            aSrcRect.Y += TodCommon.FloatRoundToInt(aClipTop * aCelHeight);
            aSrcRect.Width -= TodCommon.FloatRoundToInt((aClipLeft + aClipRight) * aCelWidth);
            aSrcRect.Height -= TodCommon.FloatRoundToInt((aClipTop + aClipBottom) * aCelHeight);  // 以上根据裁剪各方向的比例调整源矩形
            Debug.ASSERT(aSrcRect.X == aCelWidth * aFrame + TodCommon.FloatRoundToInt(aClipLeft * aCelWidth));
            Debug.ASSERT(aSrcRect.Y == aCelHeight * aEmitterDef.mImageRow + TodCommon.FloatRoundToInt(aClipTop * aCelHeight));
            Debug.ASSERT(aSrcRect.X >= 0 && aSrcRect.X < 10000);
            Debug.ASSERT(aSrcRect.Y >= 0 && aSrcRect.Y < 10000);

            if (TodCommon.TestBit((uint)aEmitterDef.mParticleFlags, (int)ParticleFlags.AlignToPixels))  // 坐标对齐至整数像素点
            {
                theParams.mPosX = TodCommon.FloatRoundToInt(theParams.mPosX);
                theParams.mPosY = TodCommon.FloatRoundToInt(theParams.mPosY);
            }

            DrawMode aDrawMode = g.mDrawMode;
            if (TodCommon.TestBit((uint)aEmitterDef.mParticleFlags, (int)ParticleFlags.Additive))  // 使用叠加模式
            {
                aDrawMode = DrawMode.Additive;
            }

            if (TodCommon.TestBit((uint)aEmitterDef.mParticleFlags, (int)ParticleFlags.Fullscreen))  // 全屏模式
            {
                SexyColor anOldColor = g.GetColor();
                DrawMode anOldDrawMode = g.mDrawMode;
                g.SetColor(theColor);
                g.SetDrawMode(aDrawMode);
                g.FillRect((int)-g.mTransX - 16384, (int)-g.mTransY - 16384, 16384 * 3, 16384 * 3);
                g.SetColor(anOldColor);
                g.SetDrawMode(anOldDrawMode);
            }
            else
            {
                Matrix4x4 aTransform = default;
                TodCommon.TodScaleRotateTransformMatrix(
                    ref aTransform,
                    theParams.mPosX,
                    theParams.mPosY,
                    theParams.mSpinPosition,
                    theParams.mParticleScale,
                    theParams.mParticleStretch * theParams.mParticleScale
                    );
                TodCommon.TodBltMatrix(g, aImage, aTransform, g.GetClipRect(), theColor, aDrawMode, aSrcRect);
                if (aEmitter.mExtraAdditiveDrawOverride)
                {
                    TodCommon.TodBltMatrix(g, aImage, aTransform, g.GetClipRect(), theColor, DrawMode.Additive, aSrcRect);
                }
            }
        }
    }
}
