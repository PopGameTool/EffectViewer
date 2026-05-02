using System;
using System.Runtime.CompilerServices;

namespace EffectViewer.TodLib.Trail
{
    public class Trail : IDataArrayItem
    {
        public InlineArray20<TrailPoint> mTrailPoints; // TodLibConstants.MAX_TRAIL_POINTS
        public int mNumTrailPoints;
        public bool mDead;
        public int mRenderOrder;
        public int mTrailAge;
        public int mTrailDuration;
        public TrailDefinition mDefinition;
        public TrailHolder mTrailHolder;
        public InlineArray4<float> mTrailInterp; // TrailTracks.NumTrailTracks
        public Vector2 mTrailCenter;
        public bool mIsAttachment;
        public SexyColor mColorOverride;
        public Image mImageOverride; // PvZ Ultimate

        uint IDataArrayItem.Id { get; set; }
        int IDataArrayItem.Index { get; init; }

        public Trail()
        {
            Reset();
        }

        public void Dispose()
        {

        }

        public void Reset()
        {
            mNumTrailPoints = 0;
            mDead = false;
            mIsAttachment = false;
            mRenderOrder = 0;
            mTrailAge = 0;
            mDefinition = null;
            mTrailDuration = 0;
            mColorOverride = SexyColor.White;
            for (int i = 0; i < 4; i++)
            {
                mTrailInterp[i] = TodCommon.RandRangeFloat(0f, 1f);
            }
            foreach (ref TrailPoint item in mTrailPoints)
            {
                item = new();
            }
            mTrailHolder = null;
            mTrailCenter = default;
            mImageOverride = null;
        }

        public void Update()
        {
            mTrailAge++;
            if (mTrailAge >= mTrailDuration)
            {
                if (TodCommon.TestBit((uint)mDefinition.mTrailFlags, (int)TrailFlags.Loops))
                {
                    mTrailAge = 0;
                }
                else
                {
                    mDead = true;
                }
            }
        }

        public void Draw(EffectViewer.TodLib.Graphics.Graphics g)
        {
            if (mDead || mNumTrailPoints < 2)
            {
                return;
            }

            float aTimeValue = mTrailAge / (float)(mTrailDuration - 1);
            int aTriangleCount = (mNumTrailPoints - 1) * 2;
            Debug.ASSERT(aTriangleCount <= TodLibConstants.MAX_TRAIL_TRIANGLES);

            InlineArray38<InlineArray3<TriVertex>> aVertArray = new(); // TodLibConstants.MAX_TRAIL_TRIANGLES

            bool aHavePrev = false;
            Vector2 aNormalPrev = default;
            for (int i = 0; i < mNumTrailPoints - 1; i++)
            {
                if (!aHavePrev)
                {
                    if (!GetNormalAtPoint(i, ref aNormalPrev))
                    {
                        continue;
                    }
                    aHavePrev = true;
                }

                Vector2 aNormalNext = default;
                Vector2 aNormalCur = aNormalPrev;
                if (!GetNormalAtPoint(i + 1, ref aNormalNext))
                {
                    aNormalNext = aNormalPrev;
                }
                else
                {
                    aNormalPrev = aNormalNext;
                }

                ref TrailPoint aPointCur = ref mTrailPoints[i];
                ref TrailPoint aPointNext = ref mTrailPoints[i + 1];
                float aUCur = 1f - i / (float)(mNumTrailPoints - 1);
                float aUNext = 1f - (i + 1) / (float)(mNumTrailPoints - 1);
                float aWidthOverLengthCur = Definition.FloatTrackEvaluate(mDefinition.mWidthOverLength, aUCur, mTrailInterp[(int)TrailTracks.WidthOverLength]);
                float aWidthOverLengthNext = Definition.FloatTrackEvaluate(mDefinition.mWidthOverLength, aUNext, mTrailInterp[(int)TrailTracks.WidthOverLength]);
                float aWidthOverTimeCur = Definition.FloatTrackEvaluate(mDefinition.mWidthOverTime, aTimeValue, mTrailInterp[(int)TrailTracks.WidthOverTime]);
                float aWidthOverTimeNext = Definition.FloatTrackEvaluate(mDefinition.mWidthOverTime, aTimeValue, mTrailInterp[(int)TrailTracks.WidthOverTime]);
                float aAlphaOverLengthCur = Definition.FloatTrackEvaluate(mDefinition.mAlphaOverLength, aUCur, mTrailInterp[(int)TrailTracks.AlphaOverLength]);
                float aAlphaOverLengthNext = Definition.FloatTrackEvaluate(mDefinition.mAlphaOverLength, aUNext, mTrailInterp[(int)TrailTracks.AlphaOverLength]);
                float aAlphaOverTimeCur = Definition.FloatTrackEvaluate(mDefinition.mAlphaOverTime, aTimeValue, mTrailInterp[(int)TrailTracks.AlphaOverTime]);
                float aAlphaOverTimeNext = Definition.FloatTrackEvaluate(mDefinition.mAlphaOverTime, aTimeValue, mTrailInterp[(int)TrailTracks.AlphaOverTime]);
                int anAlphaCur = TodCommon.ClampInt(TodCommon.FloatRoundToInt(aAlphaOverLengthCur * aAlphaOverTimeCur * mColorOverride.mAlpha), 0, 255);
                int anAlphaNext = TodCommon.ClampInt(TodCommon.FloatRoundToInt(aAlphaOverLengthNext * aAlphaOverTimeNext * mColorOverride.mAlpha), 0, 255);
                SexyColor aColorCur = mColorOverride;
                SexyColor aColorNext = mColorOverride;
                aColorCur.mAlpha = anAlphaCur;
                aColorNext.mAlpha = anAlphaNext;

                InlineArray4<Vector2> aPosition = new();
                aPosition[0].X = mTrailCenter.X + aPointCur.aPos.X + (aNormalCur.X * aWidthOverLengthCur * aWidthOverTimeCur);
                aPosition[0].Y = mTrailCenter.Y + aPointCur.aPos.Y + (aNormalCur.Y * aWidthOverLengthCur * aWidthOverTimeCur);
                aPosition[1].X = mTrailCenter.X + aPointCur.aPos.X + (-aNormalCur.X * aWidthOverLengthCur * aWidthOverTimeCur);
                aPosition[1].Y = mTrailCenter.Y + aPointCur.aPos.Y + (-aNormalCur.Y * aWidthOverLengthCur * aWidthOverTimeCur);
                aPosition[2].X = mTrailCenter.X + aPointNext.aPos.X + (aNormalNext.X * aWidthOverLengthNext * aWidthOverTimeNext);
                aPosition[2].Y = mTrailCenter.Y + aPointNext.aPos.Y + (aNormalNext.Y * aWidthOverLengthNext * aWidthOverTimeNext);
                aPosition[3].X = mTrailCenter.X + aPointNext.aPos.X + (-aNormalNext.X * aWidthOverLengthNext * aWidthOverTimeNext);
                aPosition[3].Y = mTrailCenter.Y + aPointNext.aPos.Y + (-aNormalNext.Y * aWidthOverLengthNext * aWidthOverTimeNext);
                
                int aVertCur = i * 2;
                int aVertNext = aVertCur + 1;
                aVertArray[aVertCur][0].Position.X = aPosition[0].X;
                aVertArray[aVertCur][0].Position.Y = aPosition[0].Y;
                aVertArray[aVertCur][0].TextureCoordinate.X = aUCur;
                aVertArray[aVertCur][0].TextureCoordinate.Y = 1f;
                aVertArray[aVertCur][0].Color = aColorCur;
                aVertArray[aVertCur][1].Position.X = aPosition[1].X;
                aVertArray[aVertCur][1].Position.Y = aPosition[1].Y;
                aVertArray[aVertCur][1].TextureCoordinate.X = aUCur;
                aVertArray[aVertCur][1].TextureCoordinate.Y = 0f;
                aVertArray[aVertCur][1].Color = aColorCur;
                aVertArray[aVertCur][2].Position.X = aPosition[2].X;
                aVertArray[aVertCur][2].Position.Y = aPosition[2].Y;
                aVertArray[aVertCur][2].TextureCoordinate.X = aUNext;
                aVertArray[aVertCur][2].TextureCoordinate.Y = 1f;
                aVertArray[aVertCur][2].Color = aColorNext;
                aVertArray[aVertNext][0].Position.X = aPosition[2].X;
                aVertArray[aVertNext][0].Position.Y = aPosition[2].Y;
                aVertArray[aVertNext][0].TextureCoordinate.X = aUNext;
                aVertArray[aVertNext][0].TextureCoordinate.Y = 1f;
                aVertArray[aVertNext][0].Color = aColorNext;
                aVertArray[aVertNext][1].Position.X = aPosition[1].X;
                aVertArray[aVertNext][1].Position.Y = aPosition[1].Y;
                aVertArray[aVertNext][1].TextureCoordinate.X = aUCur;
                aVertArray[aVertNext][1].TextureCoordinate.Y = 0f;
                aVertArray[aVertNext][1].Color = aColorCur;
                aVertArray[aVertNext][2].Position.X = aPosition[3].X;
                aVertArray[aVertNext][2].Position.Y = aPosition[3].Y;
                aVertArray[aVertNext][2].TextureCoordinate.X = aUNext;
                aVertArray[aVertNext][2].TextureCoordinate.Y = 0f;
                aVertArray[aVertNext][2].Color = aColorNext;
            }

            Image image = mImageOverride ?? ResourceHandler.GetImage(mDefinition.mImage);
            if (image != null)
            {
                g.DrawTrianglesTex(image, ((Span<InlineArray3<TriVertex>>)aVertArray)[..aTriangleCount]);
            }
        }

        public void AddPoint(float x, float y)
        {
            int aMaxPoints = TodCommon.ClampInt(mDefinition.mMaxPoints, 2, TodLibConstants.MAX_TRAIL_POINTS);
            if (mNumTrailPoints > 0)
            {
                ref TrailPoint aPoint = ref mTrailPoints[mNumTrailPoints - 1];
                float aDistance = TodCommon.Distance2D(x, y, aPoint.aPos.X, aPoint.aPos.Y);
                if (aDistance < mDefinition.mMinPointDistance)
                {
                    return;  // 距离上次记录的轨迹点的距离不能小于规定的最小值
                }
            }

            // 当已有轨迹点数量达到上限时，舍弃最早的一个轨迹点
            if (mNumTrailPoints == aMaxPoints)
            {
                Span<TrailPoint> aSrcFrames = mTrailPoints[1..mNumTrailPoints];
                Span<TrailPoint> aDestFrames = mTrailPoints[..(mNumTrailPoints - 1)];
                aSrcFrames.CopyTo(aDestFrames);
                mNumTrailPoints--;
            }

            ref TrailPoint aNewPoint = ref mTrailPoints[mNumTrailPoints];
            aNewPoint.aPos.X = x;
            aNewPoint.aPos.Y = y;
            mNumTrailPoints++;
        }

        public bool GetNormalAtPoint(int nIndex, ref Vector2 theNormal)
        {
            Vector2 aDirection;
            if (nIndex == 0)
            {
                Vector2 aToNext = mTrailPoints[nIndex + 1].aPos - mTrailPoints[nIndex].aPos;
                aDirection = aToNext.GetPerp();
            }
            else if (nIndex == mNumTrailPoints - 1)
            {
                Vector2 aFromPrev = mTrailPoints[nIndex].aPos - mTrailPoints[nIndex - 1].aPos;
                aDirection = aFromPrev.GetPerp();
            }
            else
            {
                Vector2 aToNext = mTrailPoints[nIndex + 1].aPos - mTrailPoints[nIndex].aPos;
                Vector2 aToPrev = mTrailPoints[nIndex - 1].aPos - mTrailPoints[nIndex].aPos;
                aDirection = aToPrev.NormalizeSafe() + aToNext.NormalizeSafe();
            }

            float aMag = aDirection.Length();
            if (TodCommon.FloatApproxEqual(aMag, 0f))
            {
                return false;
            }

            theNormal.X = aDirection.X / aMag;
            theNormal.Y = aDirection.Y / aMag;
            return true;
        }
    }
}
