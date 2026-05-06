using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace EffectViewer.TodLib.Reanim
{
    public class Reanimation : IDataArrayItem
    {
        public const string Attacher = "attacher__";

        public static bool mInterpolate = true;

        public static string ReanimTrackId_fullscreen = ReanimatorXnaHelpers.ReanimatorTrackNameToId("fullscreen");

        public static string ReanimTrackId__ground = ReanimatorXnaHelpers.ReanimatorTrackNameToId("_ground");

        public static string ReanimTrackId_anim_walk = ReanimatorXnaHelpers.ReanimatorTrackNameToId("anim_walk");

        public static string ReanimTrackId_anim_crawl = ReanimatorXnaHelpers.ReanimatorTrackNameToId("anim_crawl");

        public static string ReanimTrackIdEmpty = ReanimatorXnaHelpers.ReanimatorTrackNameToId("");

        private static readonly Dictionary<string, string> lowercaseCache = new();

        private static readonly Dictionary<string, AttacherInfo> cachedAttacherInfo = new();

        public string mReanimationType;
        public float mAnimTime;
        public float mAnimRate;
        public ReanimatorDefinition mDefinition;
        public ReanimLoopType mLoopType;
        public bool mDead;
        public int mFrameStart;
        public int mFrameCount;
        public int mFrameBasePose;
        public Matrix4x4 mOverlayMatrix;
        public SexyColor mColorOverride;
        public ReanimatorTrackInstance[] mTrackInstances;
        public int mLoopCount;
        public ReanimationHolder mReanimationHolder;
        public bool mIsAttachment;
        public int mRenderOrder;
        public SexyColor mExtraAdditiveColor;
        public bool mEnableExtraAdditiveDraw;
        public SexyColor mExtraOverlayColor;
        public bool mEnableExtraOverlayDraw;
        public float mLastFrameTime;
        public FilterEffectType mFilterEffect;

        uint IDataArrayItem.Id { get; set; }
        int IDataArrayItem.Index { get; init; }

        private EffectSystem mEffectSystem => mReanimationHolder?.mEffectSystem;

        public Reanimation()
        {
            Reset();
        }

        public override string ToString()
        {
            return mReanimationType.ToString();
        }

        public static string ToLower(string s)
        {
            string text;
            if (!lowercaseCache.TryGetValue(s, out text))
            {
                text = s.ToLower();
                lowercaseCache.Add(s, text);
            }

            return text;
        }

        public void Dispose()
        {
            ReanimationDie();
            ReanimationDelete();
        }

        public void Reset()
        {
            mAnimTime = 0f;
            mAnimRate = 12f;
            mDefinition = null;
            mLoopType = ReanimLoopType.PlayOnce;
            mLastFrameTime = -1f;
            mDead = false;
            mFrameStart = 0;
            mFrameCount = 0;
            mFrameBasePose = -1;
            mOverlayMatrix = Matrix4x4.Identity;
            mColorOverride = SexyColor.White;
            mExtraAdditiveColor = SexyColor.White;
            mEnableExtraAdditiveDraw = false;
            mExtraOverlayColor = SexyColor.White;
            mEnableExtraOverlayDraw = false;
            mLoopCount = 0;
            mIsAttachment = false;
            mRenderOrder = 0;
            mReanimationHolder = null;
            mTrackInstances = null;
            mFilterEffect = FilterEffectType.None;
            mReanimationType = null;
        }

        public void ReanimationInitialize(float theX, float theY, string theDefinition)
        {
            Debug.ASSERT(mTrackInstances == null);
            mDead = false;
            SetPosition(theX, theY);
            mDefinition = ReanimatorXnaHelpers.gReanimatorDefArray[theDefinition];
            mAnimRate = mDefinition.mFPS;
            mLastFrameTime = -1f;

            if (mDefinition.mTrackCount != 0)
            {
                mFrameCount = mDefinition.mTracks[0].mTransformCount;
                mTrackInstances = new ReanimatorTrackInstance[mDefinition.mTrackCount];  // 申请动画轨道实例数组所需的内存
                for (int aTrackIndex = 0; aTrackIndex < mDefinition.mTrackCount; aTrackIndex++)  // 遍历初始化数组中每个轨道实例
                {
                    mTrackInstances[aTrackIndex].Reset();
                    // 预计算 attacher 标志，避免 Update 热循环中每帧做字符串比较
                    string aName = mDefinition.mTracks[aTrackIndex].mName;
                    mTrackInstances[aTrackIndex].mIsAttacher =
                        !string.IsNullOrEmpty(aName) &&
                        aName.StartsWith("attacher__", StringComparison.OrdinalIgnoreCase);
                }
            }
            else
            {
                mFrameCount = 0;
            }
        }

        public void ReanimationInitializeType(float theX, float theY, string theReanimType)
        {
            ReanimatorXnaHelpers.ReanimatorEnsureDefinitionLoaded(theReanimType, false);
            mReanimationType = theReanimType;
            ReanimationInitialize(theX, theY, theReanimType);
        }

        public void ReanimationDie()
        {
            if (!mDead)
            {
                mDead = true;
                if (mEffectSystem == null)
                {
                    return;
                }

                for (int aTrackIndex = 0; aTrackIndex < mDefinition.mTrackCount; aTrackIndex++)
                {
                    Debug.ASSERT(mTrackInstances != null);
                    GlobalMembersAttachment.AttachmentDie(mEffectSystem, ref mTrackInstances[aTrackIndex].mAttachmentID);
                }
            }
        }

        public void Update()
        {
            if (mFrameCount == 0 || mDead)
            {
                return;
            }

            Debug.ASSERT(float.IsFinite(mAnimRate));
            mLastFrameTime = mAnimTime;  // 更新上一帧的循环率
            mAnimTime += ReanimatorXnaHelpers.SECONDS_PER_UPDATE * mAnimRate / mFrameCount;  // 更新当前循环率
            
            if (mAnimRate > 0f)
            {
                if (mLoopType == ReanimLoopType.Loop ||
                    mLoopType == ReanimLoopType.LoopFullLastFrame)
                {
                    while (mAnimTime >= 1f)
                    {
                        mLoopCount++;
                        mAnimTime -= 1f;
                    }
                }
                else if (mLoopType == ReanimLoopType.PlayOnce ||
                    mLoopType == ReanimLoopType.PlayOnceFullLastFrame)
                {
                    if (mAnimTime >= 1f)
                    {
                        mLoopCount = 1;
                        mAnimTime = 1f;
                        mDead = true;
                    }
                }
                else if (mLoopType == ReanimLoopType.PlayOnceAndHold ||
                    mLoopType == ReanimLoopType.PlayOnceFullLastFrameAndHold)
                {
                    if (mAnimTime >= 1f)
                    {
                        mLoopCount = 1;
                        mAnimTime = 1f;
                    }
                }
                else
                {
                    Debug.ASSERT(false);
                }
            }
            else
            {
                if (mLoopType == ReanimLoopType.Loop ||
                    mLoopType == ReanimLoopType.LoopFullLastFrame)
                {
                    while (mAnimTime < 0f)
                    {
                        mLoopCount++;
                        mAnimTime += 1f;
                    }
                }
                else if (mLoopType == ReanimLoopType.PlayOnce ||
                    mLoopType == ReanimLoopType.PlayOnceFullLastFrame)
                {
                    if (mAnimTime < 0f)
                    {
                        mLoopCount = 1;
                        mAnimTime = 0f;
                        mDead = true;
                    }
                }
                else if (mLoopType == ReanimLoopType.PlayOnceAndHold ||
                    mLoopType == ReanimLoopType.PlayOnceFullLastFrameAndHold)
                {
                    if (mAnimTime < 0f)
                    {
                        mLoopCount = 1;
                        mAnimTime = 0f;
                    }
                }
                else
                {
                    Debug.ASSERT(false);
                }
            }

            for (int aTrackIndex = 0; aTrackIndex < mDefinition.mTrackCount; aTrackIndex++)
            {
                ref ReanimatorTrackInstance aTrack = ref mTrackInstances[aTrackIndex];
                if (aTrack.mBlendCounter > 0)
                {
                    aTrack.mBlendCounter--;  // 更新轨道的混合倒计时
                }

                if (aTrack.mShakeOverride != 0f)  // 更新轨道震动
                {
                    aTrack.mShakeX = TodCommon.RandRangeFloat(-aTrack.mShakeOverride, aTrack.mShakeOverride);
                    aTrack.mShakeY = TodCommon.RandRangeFloat(-aTrack.mShakeOverride, aTrack.mShakeOverride);
                }

                if (aTrack.mIsAttacher)  // 预计算标志，避免每帧字符串比较
                {
                    UpdateAttacherTrack(aTrackIndex);
                }

                if (aTrack.mAttachmentID != AttachmentID.Null)
                {
                    GetAttachmentOverlayMatrix(aTrackIndex, out Matrix4x4 aOverlayMatrix);
                    GlobalMembersAttachment.AttachmentUpdateAndSetMatrix(mEffectSystem, ref aTrack.mAttachmentID, aOverlayMatrix);
                }
            }
        }

        public void Draw(EffectViewer.TodLib.Graphics.Graphics g)
        {
            DrawRenderGroup(g, ReanimatorXnaHelpers.RENDER_GROUP_NORMAL);
        }

        public void DrawRenderGroup(EffectViewer.TodLib.Graphics.Graphics g, int theRenderGroup)
        {
            if (mDead)
            {
                return;
            }

            for (int aTrackIndex = 0; aTrackIndex < mDefinition.mTrackCount; aTrackIndex++)
            {
                ref ReanimatorTrackInstance aTrackInstance = ref mTrackInstances[aTrackIndex];
                if (aTrackInstance.mRenderGroup == theRenderGroup)
                {
                    bool aTrackDrawn = DrawTrack(g, aTrackIndex, theRenderGroup);
                    if (aTrackInstance.mAttachmentID != AttachmentID.Null && mEffectSystem != null)
                    {
                        GlobalMembersAttachment.AttachmentDraw(mEffectSystem, aTrackInstance.mAttachmentID, g, !aTrackDrawn);
                    }
                }
            }
        }

        public bool DrawTrack(EffectViewer.TodLib.Graphics.Graphics g, int theTrackIndex, int theRenderGroup)
        {
            ref ReanimatorTrackInstance aTrackInstance = ref mTrackInstances[theTrackIndex];  // 目标轨道的指针
            GetCurrentTransform(theTrackIndex, out ReanimatorTransform aTransform);  // 取得当前动画变换
            int aImageFrame = TodCommon.FloatRoundToInt(aTransform.mFrame);  // 图像在贴图中所处的份数
            if (aImageFrame < 0f)  // 不存在图像时，返回
            {
                return false;
            }

            SexyColor aColor = aTrackInstance.mTrackColor;
            if (!aTrackInstance.mIgnoreColorOverride)  // 除非轨道无视动画的覆写颜色
            {
                aColor = TodCommon.ColorsMultiply(aColor, mColorOverride);  // 将轨道颜色与动画的覆写颜色进行正片叠底混合
            }
            if (g.GetColorizeImages())  // 若 Graphics 着色
            {
                aColor = TodCommon.ColorsMultiply(aColor, g.mColor);  // 将颜色再与 Graphics 的颜色进行正片叠底混合
            }

            int aImageAlpha = TodCommon.ClampInt(TodCommon.FloatRoundToInt(aTransform.mAlpha * aColor.mAlpha), 0, 255);
            if (aImageAlpha <= 0)  // 当图像完全透明时，返回
            {
                return false;
            }

            aColor.mAlpha = aImageAlpha;

            SexyColor aExtraAdditiveColor = SexyColor.Black;
            if (mEnableExtraAdditiveDraw)  // 如果动画启用额外叠加颜色（高亮）
            {
                aExtraAdditiveColor = mExtraAdditiveColor;
                aExtraAdditiveColor.mAlpha = TodCommon.ColorComponentMultiply(mExtraAdditiveColor.mAlpha, aImageAlpha);
            }

            SexyColor aExtraOverlayColor = SexyColor.Black;
            if (mEnableExtraOverlayDraw)
            {
                aExtraOverlayColor = mExtraOverlayColor;
                aExtraOverlayColor.mAlpha = TodCommon.ColorComponentMultiply(mExtraOverlayColor.mAlpha, aImageAlpha);
            }

            Rectangle aClipRect = g.mClipRect;
            if (aTrackInstance.mIgnoreClipRect)  // 如果轨道无视裁剪矩形
            {
                aClipRect = new Rectangle(-16384, -16384, 16384 * 3, 16384 * 3);  // 裁剪矩形重置为屏幕矩形
            }

            Image aImage = ResourceHandler.GetImage(aTransform.mImage); // ReanimAtlasImage 在此版本不存在

            Matrix4x4 aMatrix = Matrix4x4.Identity;
            bool aFullScreen = false;
            if (aImage != null)  // 如果存在贴图。此处若上一步中图集编号非法，则可能导致崩溃
            {
                int aCelWidth = aImage.GetCelWidth();
                int aCelHeight = aImage.GetCelHeight();
                TodCommon.SexyMatrix3Translation(ref aMatrix, aCelWidth * 0.5f, aCelHeight * 0.5f);  // 将矩阵变换的坐标设定在贴图的中心位置
            }
            else if (aTransform.mFont != null && !string.IsNullOrEmpty(aTransform.mText))  // 如果存在字体且文本不为空
            {
                Font aFont = ResourceHandler.GetFont(aTransform.mFont);
                if (aFont == null)
                {
                    return false;
                }

                int aWidth = aFont.StringWidth(aTransform.mText);
                TodCommon.SexyMatrix3Translation(ref aMatrix, -aWidth * 0.5f, aFont.mAscent);
            }
            else
            {
                if (string.Compare(mDefinition.mTracks[theTrackIndex].mName, "fullscreen", StringComparison.OrdinalIgnoreCase) != 0)  // 如果既没有图像也没有文本，且不是全屏轨道
                {
                    return false;  // 无需绘制
                }

                aFullScreen = true;  // 标记全屏轨道，后续会填充一个屏幕大小的矩形
            }

            MatrixFromTransform(aTransform, out Matrix4x4 aTransformMatrix);
            TodCommon.SexyMatrix3Multiply(ref aMatrix, aTransformMatrix, aMatrix);  // 以动画变换矩阵作用 aMatrix
            TodCommon.SexyMatrix3Multiply(ref aMatrix, mOverlayMatrix, aMatrix);  // 以动画覆写矩阵作用 aMatrix
            TodCommon.SexyMatrix3Translation(ref aMatrix, aTrackInstance.mShakeX + g.mTransX - 0.5f, aTrackInstance.mShakeY + g.mTransY - 0.5f);  // 轨道震动及 g 的影响

            if (aImage != null)  // 如果轨道变换存在图像
            {
                if (aTrackInstance.mImageOverride != null)  // 如果轨道存在覆写贴图
                {
                    aImage = aTrackInstance.mImageOverride;  // 将贴图替换为覆写贴图
                }

                if (mFilterEffect != FilterEffectType.None)  // 如果动画存在滤镜
                {
                    aImage = FilterEffect.FilterEffectGetImage(aImage, mFilterEffect);  // 将贴图替换为滤镜后的贴图
                }

                while (aImageFrame >= aImage.mNumCols)
                {
                    aImageFrame -= aImage.mNumCols;  // 确保绘制的列数不会超过贴图最后一列
                }

                int aCelWidth = aImage.GetCelWidth();
                Rectangle aSrcRect = new(aCelWidth * aImageFrame, 0, aCelWidth, aImage.GetCelHeight());
                ReanimBltMatrix(g, aImage, aMatrix, aClipRect, aColor, g.mDrawMode, aSrcRect);  // 带矩阵绘制轨道图像
                if (mEnableExtraAdditiveDraw)
                {
                    ReanimBltMatrix(g, aImage, aMatrix, aClipRect, aExtraAdditiveColor, DrawMode.Additive, aSrcRect);
                }

                if (mEnableExtraOverlayDraw)
                {
                    Image aOverlayImage = FilterEffect.FilterEffectGetImage(aImage, FilterEffectType.White);
                    ReanimBltMatrix(g, aOverlayImage, aMatrix, aClipRect, aExtraOverlayColor, DrawMode.Normal, aSrcRect);
                }
            }
            else if ((!string.IsNullOrEmpty(aTrackInstance.mFontOverride) || aTransform.mFont != null) && !string.IsNullOrEmpty(aTransform.mText))  // 如果不存在图像但存在文本
            {
                string aFontId = !string.IsNullOrEmpty(aTrackInstance.mFontOverride)
                    ? aTrackInstance.mFontOverride
                    : aTransform.mFont;
                Font aFont = ResourceHandler.GetFont(aFontId);
                TodCommon.TodDrawStringMatrix(g, aFont, aMatrix, aTransform.mText, aColor);
                if (mEnableExtraAdditiveDraw)
                {
                    DrawMode aOldMode = g.GetDrawMode();  // 备份绘制模式
                    g.SetDrawMode(DrawMode.Additive);
                    TodCommon.TodDrawStringMatrix(g, aFont, aMatrix, aTransform.mText, aExtraAdditiveColor);
                    g.SetDrawMode(aOldMode);  // 还原绘制模式
                }
            }
            else if (aFullScreen)  // 不存在图像和文本，但是全屏
            {
                SexyColor aOldColor = g.GetColor();  // 备份颜色
                g.SetColor(aColor);
                g.FillRect((int)-g.mTransX - 16384, (int)-g.mTransY - 16384, 16384 * 3, 16384 * 3);
                g.SetColor(aOldColor);  // 还原颜色
            }
            return true;
        }

        public void GetCurrentTransform(int theTrackIndex, out ReanimatorTransform theTransformCurrent)
        {
            GetFrameTime(out ReanimatorFrameTime aFrameTime);
            GetTransformAtTime(theTrackIndex, out theTransformCurrent, aFrameTime);  // 结合两帧之间的自然补间取得基础变换

            ref ReanimatorTrackInstance aTrack = ref mTrackInstances[theTrackIndex];
            if (TodCommon.FloatRoundToInt(theTransformCurrent.mFrame) >= 0 && aTrack.mBlendCounter > 0)  // 若当前不为空白帧且轨道处于变换混合过程中
            {
                float aBlendFactor = aTrack.mBlendCounter / (float)aTrack.mBlendTime;
                ReanimatorXnaHelpers.BlendTransform(ref theTransformCurrent, theTransformCurrent, aTrack.mBlendTransform, aBlendFactor);
            }
        }

        public void GetTransformAtTime(int theTrackIndex, out ReanimatorTransform theTransform, in ReanimatorFrameTime theFrameTime)
        {
            Debug.ASSERT(theTrackIndex >= 0 && theTrackIndex < mDefinition.mTrackCount);
            ReanimatorTrack aTrack = mDefinition.mTracks[theTrackIndex];
            ref ReanimatorTransform aTransBefore = ref aTrack.mTransforms[theFrameTime.mAnimFrameBeforeInt];  // 前一帧的变换定义
            ref ReanimatorTransform aTransAfter = ref aTrack.mTransforms[theFrameTime.mAnimFrameAfterInt];  // 后一帧的变换定义

            theTransform.mTransX = TodCommon.FloatLerp(aTransBefore.mTransX, aTransAfter.mTransX, theFrameTime.mFraction);
            theTransform.mTransY = TodCommon.FloatLerp(aTransBefore.mTransY, aTransAfter.mTransY, theFrameTime.mFraction);
            theTransform.mSkewX = TodCommon.FloatLerp(aTransBefore.mSkewX, aTransAfter.mSkewX, theFrameTime.mFraction);
            theTransform.mSkewY = TodCommon.FloatLerp(aTransBefore.mSkewY, aTransAfter.mSkewY, theFrameTime.mFraction);
            theTransform.mScaleX = TodCommon.FloatLerp(aTransBefore.mScaleX, aTransAfter.mScaleX, theFrameTime.mFraction);
            theTransform.mScaleY = TodCommon.FloatLerp(aTransBefore.mScaleY, aTransAfter.mScaleY, theFrameTime.mFraction);
            theTransform.mAlpha = TodCommon.FloatLerp(aTransBefore.mAlpha, aTransAfter.mAlpha, theFrameTime.mFraction);
            theTransform.mImage = aTransBefore.mImage;
            theTransform.mFont = aTransBefore.mFont;
            theTransform.mText = aTransBefore.mText;

            if (aTransBefore.mFrame != -1f &&
                aTransAfter.mFrame == -1f &&
                theFrameTime.mFraction > 0f &&
                mTrackInstances[theTrackIndex].mTruncateDisappearingFrames)
            {
                theTransform.mFrame = -1f;  // 当从一个非空白帧过渡至空白帧时，若轨道设置了截断消失帧，则删去过渡的过程
            }
            else
            {
                theTransform.mFrame = aTransBefore.mFrame;
            }
        }

        public void GetFrameTime(out ReanimatorFrameTime theFrameTime)
        {
            Debug.ASSERT(mFrameStart + mFrameCount <= mDefinition.mTracks[0].mTransformCount);

            int aFrameCount;
            if (mLoopType == ReanimLoopType.PlayOnceFullLastFrame ||
                mLoopType == ReanimLoopType.LoopFullLastFrame ||
                mLoopType == ReanimLoopType.PlayOnceFullLastFrameAndHold)
            {
                aFrameCount = mFrameCount;
            }
            else
            {
                aFrameCount = mFrameCount - 1;
            }

            float aAnimPosition = mFrameStart + (aFrameCount * mAnimTime);
            float aAnimFrameBefore = MathF.Floor(aAnimPosition);
            theFrameTime.mFraction = aAnimPosition - aAnimFrameBefore;
            theFrameTime.mAnimFrameBeforeInt = (short)TodCommon.FloatRoundToInt(aAnimFrameBefore);
            if (theFrameTime.mAnimFrameBeforeInt >= mFrameStart + mFrameCount - 1)  // 如果当前处于结束的一帧
            {
                theFrameTime.mAnimFrameBeforeInt = (short)(mFrameStart + mFrameCount - 1);
                theFrameTime.mAnimFrameAfterInt = theFrameTime.mAnimFrameBeforeInt;  // 将前、后的整数帧均赋值为最后一帧
            }
            else
            {
                theFrameTime.mAnimFrameAfterInt = (short)(theFrameTime.mAnimFrameBeforeInt + 1);  // 后一整数帧等于前一整数帧的后一帧
            }

            Debug.ASSERT(theFrameTime.mAnimFrameBeforeInt >= 0 && theFrameTime.mAnimFrameAfterInt < mDefinition.mTracks[0].mTransformCount);
        }

        public int FindTrackIndex(string theTrackName)
        {
            // 优先使用预建的 O(1) 字典查找
            int aFastIdx = mDefinition.FindTrackIndexFast(theTrackName);
            if (aFastIdx >= 0)
                return aFastIdx;

            // 回退：线性扫描（字典未初始化时的保险路径）
            for (int aTrackIndex = 0; aTrackIndex < mDefinition.mTrackCount; aTrackIndex++)
            {
                if (string.Compare(mDefinition.mTracks[aTrackIndex].mName, theTrackName, StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return aTrackIndex;
                }
            }

            Debug.Log(DebugType.Error, $"Can't find track '{theTrackName}'");
            return 0;
        }

        public void AttachToAnotherReanimation(Reanimation theAttachReanim, string theTrackName)
        {
            if (theAttachReanim == null)
            {
                throw new ArgumentNullException(nameof(theAttachReanim));
            }

            if (theAttachReanim.mDefinition.mTrackCount <= 0)
            {
                return;
            }

            Debug.ASSERT(mEffectSystem != null);
            if (mEffectSystem == null)
            {
                throw new InvalidOperationException("The reanimation is not owned by an EffectSystem.");
            }

            Debug.ASSERT(ReferenceEquals(theAttachReanim.mReanimationHolder?.mEffectSystem, mEffectSystem));
            if (!ReferenceEquals(theAttachReanim.mReanimationHolder?.mEffectSystem, mEffectSystem))
            {
                throw new InvalidOperationException("Both reanimations must belong to the same EffectSystem.");
            }

            if (!mEffectSystem.mReanimationHolder.mReanimations.DataArrayContains(this) ||
                !mEffectSystem.mReanimationHolder.mReanimations.DataArrayContains(theAttachReanim))
            {
                throw new InvalidOperationException("Both reanimations must be active in the same EffectSystem.");
            }

            if (theAttachReanim.mFrameBasePose == -1)
            {
                theAttachReanim.mFrameBasePose = theAttachReanim.mFrameStart;  // 将当前动作的起始帧作为变换基准帧
            }

            GlobalMembersAttachment.AttachReanim(mEffectSystem, ref theAttachReanim.GetTrackInstanceByName(theTrackName).mAttachmentID, this, 0f, 0f);
        }

        public void GetAttachmentOverlayMatrix(int theTrackIndex, out Matrix4x4 theOverlayMatrix)
        {
            GetCurrentTransform(theTrackIndex, out ReanimatorTransform aTransform);  // 取得含混合、不含覆写的自然变换
            MatrixFromTransform(aTransform, out Matrix4x4 aTransformMatrix);
            TodCommon.SexyMatrix3Multiply(ref aTransformMatrix, mOverlayMatrix, aTransformMatrix);  // 以动画覆写矩阵作用于动画变换矩阵

            GetTrackBasePoseMatrix(theTrackIndex, out Matrix4x4 aBasePoseMatrix);  // 取得轨道基础形态的变换矩阵
            TodCommon.SexyMatrix3Inverse(aBasePoseMatrix, out Matrix4x4 aBasePoseMatrixInv);  // 取得基础形态矩阵的逆
            theOverlayMatrix = aBasePoseMatrixInv * aTransformMatrix; // 和PC版不同，这里是行向量矩阵，右乘
        }

        public void SetFramesForLayer(string theTrackName)
        {
            if (mAnimRate >= 0f)
            {
                mAnimTime = 0f;
            }
            else
            {
                mAnimTime = 0.9999999f;
            }

            mLastFrameTime = -1f;
            GetFramesForLayer(theTrackName, out mFrameStart, out mFrameCount);
        }

        public static void MatrixFromTransform(in ReanimatorTransform theTransform, out Matrix4x4 theMatrix)
        {
            // 将倾斜的角度转化为弧度
            float aSkewX = -TodCommon.DEG_TO_RAD * theTransform.mSkewX;
            float aSkewY = -TodCommon.DEG_TO_RAD * theTransform.mSkewY;
            float aCosX = MathF.Cos(aSkewX);
            float aSinX = MathF.Sin(aSkewX);
            // 当 skewX == skewY（纯旋转）时复用已算好的 cos/sin，避免重复三角函数调用
            float aCosY = theTransform.mSkewX == theTransform.mSkewY ? aCosX : MathF.Cos(aSkewY);
            float aSinY = theTransform.mSkewX == theTransform.mSkewY ? aSinX : MathF.Sin(aSkewY);
            theMatrix = new Matrix4x4
            {
                M11 = aCosX * theTransform.mScaleX,
                M12 = -aSinX * theTransform.mScaleX,
                M13 = 0f,
                M14 = 0f,
                M21 = aSinY * theTransform.mScaleY,
                M22 = aCosY * theTransform.mScaleY,
                M23 = 0f,
                M24 = 0f,
                M31 = 0f,
                M32 = 0f,
                M33 = 1f,
                M34 = 0f,
                M41 = theTransform.mTransX,
                M42 = theTransform.mTransY,
                M43 = 0f,
                M44 = 1f
            };
        }

        public bool TrackExists(string theTrackName)
        {
            for (int aTrackIndex = 0; aTrackIndex < mDefinition.mTrackCount; aTrackIndex++)
            {
                if (string.Compare(mDefinition.mTracks[aTrackIndex].mName, theTrackName, StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return true;
                }
            }

            return false;
        }

        public void StartBlend(byte theBlendTime)
        {
            for (int aTrackIndex = 0; aTrackIndex < mDefinition.mTrackCount; aTrackIndex++)
            {
                GetCurrentTransform(aTrackIndex, out ReanimatorTransform aTransform);
                if (TodCommon.FloatRoundToInt(aTransform.mFrame) >= 0)  // 若当前轨道当前不处于空白帧
                {
                    ref ReanimatorTrackInstance aTrackInstance = ref mTrackInstances[aTrackIndex];
                    aTrackInstance.mBlendTransform = aTransform;  // 记录当前变换为混合的初始（源）变换
                    aTrackInstance.mBlendTime = theBlendTime;
                    aTrackInstance.mBlendCounter = theBlendTime;
                    aTrackInstance.mBlendTransform.mFont = null;
                    aTrackInstance.mBlendTransform.mText = "";
                    aTrackInstance.mBlendTransform.mImage = null;
                }
            }
        }

        public void SetShakeOverride(string theTrackName, float theShakeAmount)
        {
            GetTrackInstanceByName(theTrackName).mShakeOverride = theShakeAmount;
        }

        public void SetPosition(float theX, float theY)
        {
            mOverlayMatrix.M41 = theX;
            mOverlayMatrix.M42 = theY;
        }

        public void OverrideScale(float theScaleX, float theScaleY)
        {
            mOverlayMatrix.M11 = theScaleX;
            mOverlayMatrix.M22 = theScaleY;
        }

        public float GetTrackVelocity(string theTrackName)
        {
            GetFrameTime(out ReanimatorFrameTime aFrameTime);
            int aTrackIndex = FindTrackIndex(theTrackName);
            Debug.ASSERT(aTrackIndex >= 0 && aTrackIndex < mDefinition.mTrackCount);

            ReanimatorTrack aTrack = mDefinition.mTracks[aTrackIndex];
            float aDis = aTrack.mTransforms[aFrameTime.mAnimFrameAfterInt].mTransX - aTrack.mTransforms[aFrameTime.mAnimFrameBeforeInt].mTransX;
            return aDis * ReanimatorXnaHelpers.SECONDS_PER_UPDATE * mAnimRate;  // 瞬时速率 = 两帧间的横坐标之差 * 一帧的时长 * 动画速率
        }

        public void SetImageOverride(string theTrackName, Image theImage)
        {
            GetTrackInstanceByName(theTrackName).mImageOverride = theImage;
        }

        public Image GetImageOverride(string theTrackName)
        {
            return GetTrackInstanceByName(theTrackName).mImageOverride;
        }

        public void SetFontOverride(string theTrackName, string theFontId)
        {
            GetTrackInstanceByName(theTrackName).mFontOverride = string.IsNullOrWhiteSpace(theFontId) ? null : theFontId;
        }

        public string GetFontOverride(string theTrackName)
        {
            return GetTrackInstanceByName(theTrackName).mFontOverride;
        }

        public void ShowOnlyTrack(string theTrackName)
        {
            for (int i = 0; i < mDefinition.mTrackCount; i++)
            {
                mTrackInstances[i].mRenderGroup =
                    string.Compare(mDefinition.mTracks[i].mName, theTrackName, StringComparison.OrdinalIgnoreCase) == 0 ?
                    ReanimatorXnaHelpers.RENDER_GROUP_NORMAL :
                    ReanimatorXnaHelpers.RENDER_GROUP_HIDDEN;
            }
        }

        public void GetTrackMatrix(int theTrackIndex, out Matrix4x4 theMatrix)
        {
            ref ReanimatorTrackInstance aTrackInstance = ref mTrackInstances[theTrackIndex];
            GetCurrentTransform(theTrackIndex, out ReanimatorTransform aTransform);
            int aImageFrame = TodCommon.FloatRoundToInt(aTransform.mFrame);
            Image aImage = ResourceHandler.GetImage(aTransform.mImage);

            theMatrix = Matrix4x4.Identity;
            if (aImage != null && aImageFrame >= 0)
            {
                int aCelWidth = aImage.GetCelWidth();
                int aCelHeight = aImage.GetCelHeight();
                TodCommon.SexyMatrix3Translation(ref theMatrix, aCelWidth * 0.5f, aCelHeight * 0.5f);  // 将矩阵变换的坐标设定在贴图的中心位置
            }
            else if (aTransform.mFont != null && !string.IsNullOrEmpty(aTransform.mText))
            {
                Font aFont = ResourceHandler.GetFont(aTransform.mFont);
                if (aFont != null)
                {
                    TodCommon.SexyMatrix3Translation(ref theMatrix, 0f, aFont.mAscent);
                }
            }

            MatrixFromTransform(aTransform, out Matrix4x4 aTransformMatrix);
            TodCommon.SexyMatrix3Multiply(ref theMatrix, aTransformMatrix, theMatrix);  // 以动画变换矩阵作用 theMatrix
            TodCommon.SexyMatrix3Multiply(ref theMatrix, mOverlayMatrix, theMatrix);  // 以动画覆写矩阵作用 theMatrix
            TodCommon.SexyMatrix3Translation(ref theMatrix, aTrackInstance.mShakeX - 0.5f, aTrackInstance.mShakeY - 0.5f);  // 轨道震动的影响
        }

        public void AssignRenderGroupToTrack(string theTrackName, int theRenderGroup)
        {
            for (int i = 0; i < mDefinition.mTrackCount; i++)
            {
                if (string.Compare(mDefinition.mTracks[i].mName, theTrackName, StringComparison.OrdinalIgnoreCase) == 0)
                {
                    mTrackInstances[i].mRenderGroup = theRenderGroup;  // 仅设置首个名称恰好为 theTrackName 的轨道
                    return;
                }
            }
        }

        public void AssignRenderGroupToPrefix(string theTrackName, int theRenderGroup)
        {
            for (int i = 0; i < mDefinition.mTrackCount; i++)
            {
                string aTrackName = mDefinition.mTracks[i].mName;
                if (aTrackName.StartsWith(theTrackName, StringComparison.OrdinalIgnoreCase))  // 轨道名称长度必须不小于指定前缀长度
                {
                    mTrackInstances[i].mRenderGroup = theRenderGroup;
                }
            }
        }

        public int GetCurrentFrame()
        {
            GetFrameTime(out ReanimatorFrameTime aFrameTime);
            return aFrameTime.mAnimFrameAfterInt;
        }

        public int GetRenderGroupByPrefix(string theTrackName)
        {
            if (theTrackName.Length <= 0)
            {
                return -2;
            }

            for (int i = 0; i < mDefinition.mTrackCount; i++)
            {
                string aTrackName = mDefinition.mTracks[i].mName;
                if (aTrackName.StartsWith(theTrackName, StringComparison.OrdinalIgnoreCase))  // 轨道名称长度必须不小于指定前缀长度
                {
                    return mTrackInstances[i].mRenderGroup;
                }
            }

            return -2;
        }

        public void PropogateColorToAttachments()
        {
            if (mEffectSystem == null)
            {
                return;
            }

            for (int i = 0; i < mDefinition.mTrackCount; i++)
            {
                GlobalMembersAttachment.AttachmentPropogateColor(mEffectSystem, mTrackInstances[i].mAttachmentID, mColorOverride, mEnableExtraAdditiveDraw, mExtraAdditiveColor, mEnableExtraOverlayDraw, mExtraOverlayColor);
            }
        }

        public bool ShouldTriggerTimedEvent(float theEventTime)
        {
            Debug.ASSERT(theEventTime >= 0f && theEventTime <= 1f);
            if (mFrameCount == 0 || mLastFrameTime < 0f || mAnimRate <= 0f)  // 没有动画或倒放或未播放
            {
                return false;
            }

            if (mAnimTime >= mLastFrameTime)  // 一般情况下，可触发的范围为 [mLastFrameTime, mAnimTime]
            {
                return theEventTime >= mLastFrameTime && theEventTime < mAnimTime;
            }
            else  // 若动画正好完成一次循环而重新进入下一次循环，则可触发的范围为 [0, mAnimTime] ∪ [mLastFrameTime, 1]
            {
                return theEventTime >= mLastFrameTime || theEventTime < mAnimTime;
            }
        }

        public string GetCurrentTrackImage(string theTrackName)
        {
            int aTrackIndex = FindTrackIndex(theTrackName);
            GetCurrentTransform(aTrackIndex, out ReanimatorTransform aTransform);
            return aTransform.mImage;
        }

        public ref AttachEffect AttachParticleToTrack(string theTrackName, TodParticleSystem theParticleSystem, float thePosX, float thePosY)
        {
            if (theParticleSystem != null)
            {
                Debug.ASSERT(mEffectSystem != null);
                if (mEffectSystem == null)
                {
                    throw new InvalidOperationException("The reanimation is not owned by an EffectSystem.");
                }

                Debug.ASSERT(ReferenceEquals(theParticleSystem.mParticleHolder?.mEffectSystem, mEffectSystem));
                if (!ReferenceEquals(theParticleSystem.mParticleHolder?.mEffectSystem, mEffectSystem))
                {
                    throw new InvalidOperationException("The particle system must belong to the same EffectSystem.");
                }

                if (!mEffectSystem.mReanimationHolder.mReanimations.DataArrayContains(this) ||
                    !mEffectSystem.mParticleHolder.mParticleSystems.DataArrayContains(theParticleSystem))
                {
                    throw new InvalidOperationException("The reanimation and particle system must be active in the same EffectSystem.");
                }
            }

            int aTrackIndex = FindTrackIndex(theTrackName);
            ref ReanimatorTrackInstance reanimatorTrackInstance = ref mTrackInstances[aTrackIndex];
            GetTrackBasePoseMatrix(aTrackIndex, out Matrix4x4 aBasePoseMatrix);  // 取得轨道基础形态的变换矩阵
            Vector2 aPosition = Vector2.Transform(new Vector2(thePosX, thePosY), aBasePoseMatrix);  // 以基础形态的矩阵变换位置向量
            return ref GlobalMembersAttachment.AttachParticle(mEffectSystem, ref reanimatorTrackInstance.mAttachmentID, theParticleSystem, aPosition.X, aPosition.Y);
        }

        public void GetTrackBasePoseMatrix(int theTrackIndex, out Matrix4x4 theBasePoseMatrix)
        {
            theBasePoseMatrix = Matrix4x4.Identity;
            if (mFrameBasePose == ReanimatorXnaHelpers.NO_BASE_POSE)
            {
                return;
            }

            int aBasePos = mFrameBasePose == -1 ? mFrameStart : mFrameBasePose;
            ReanimatorFrameTime aStartTime = new()
            {
                mFraction = 0f,
                mAnimFrameBeforeInt = aBasePos,
                mAnimFrameAfterInt = aBasePos + 1
            };
            GetTransformAtTime(theTrackIndex, out ReanimatorTransform aTransformStart, aStartTime);
            MatrixFromTransform(aTransformStart, out theBasePoseMatrix);
        }

        public bool IsTrackShowing(string theTrackName)
        {
            GetFrameTime(out ReanimatorFrameTime aFrameTime);
            int aTrackIndex = FindTrackIndex(theTrackName);
            Debug.ASSERT(aTrackIndex >= 0 && aTrackIndex < mDefinition.mTrackCount);

            return mDefinition.mTracks[aTrackIndex].mTransforms[aFrameTime.mAnimFrameAfterInt].mFrame >= 0f;  // 返回下一整数帧是否存在图像
        }

        public void SetTruncateDisappearingFrames(string theTrackName, bool theTruncateDisappearingFrames)
        {
            if (string.IsNullOrEmpty(theTrackName))
            {
                for (int aTrackIndex = 0; aTrackIndex < mDefinition.mTrackCount; aTrackIndex++)  // 依次设置每一轨道
                {
                    mTrackInstances[aTrackIndex].mTruncateDisappearingFrames = theTruncateDisappearingFrames;
                }
            }
            else
            {
                GetTrackInstanceByName(theTrackName).mTruncateDisappearingFrames = theTruncateDisappearingFrames;
            }
        }

        public void PlayReanim(string theTrackName, ReanimLoopType theLoopType, byte theBlendTime, float theAnimRate)
        {
            if (theBlendTime > 0)  // 当需要补间过渡时，开始混合
            {
                StartBlend(theBlendTime);
            }

            if (theAnimRate != 0f)  // 当指定的速率为 0 时，表示不改变原有动画速率
            {
                mAnimRate = theAnimRate;
            }

            mLoopType = theLoopType;
            mLoopCount = 0;
            SetFramesForLayer(theTrackName);
        }

        public void ReanimationDelete()
        {
            Debug.ASSERT(mDead);
            if (mTrackInstances != null)
            {
                Array.Clear(mTrackInstances);  // 由 GC 回收动画轨道的内存区域
                mTrackInstances = null;
            }
        }

        public ref ReanimatorTrackInstance GetTrackInstanceByName(string theTrackName)
        {
            return ref mTrackInstances[FindTrackIndex(theTrackName)];
        }

        public void GetFramesForLayer(string theTrackName, out int theFrameStart, out int theFrameCount)
        {
            if (mDefinition.mTrackCount == 0)  // 如果动画没有轨道
            {
                theFrameStart = 0;
                theFrameCount = 0;
                return;
            }

            int aTrackIndex = FindTrackIndex(theTrackName);
            Debug.ASSERT(aTrackIndex >= 0 && aTrackIndex < mDefinition.mTrackCount);
            ReanimatorTrack aTrack = mDefinition.mTracks[aTrackIndex];
            theFrameStart = 0;
            theFrameCount = 1;
            for (short i = 0; i < aTrack.mTransformCount; i++)
            {
                if (aTrack.mTransforms[i].mFrame >= 0f)
                {
                    theFrameStart = i;  // 取轨道上的首个非空白帧作为起始帧
                    break;
                }
            }

            for (int j = theFrameStart; j < aTrack.mTransformCount; j++)
            {
                if (aTrack.mTransforms[j].mFrame >= 0f)
                {
                    theFrameCount = (short)(j - theFrameStart + 1);  // 取从起始帧至轨道最后一个非空白帧之间为帧数量
                }
            }
        }

        public void UpdateAttacherTrack(int theTrackIndex)
        {
            ref ReanimatorTrackInstance aTrackInstance = ref mTrackInstances[theTrackIndex];
            GetCurrentTransform(theTrackIndex, out ReanimatorTransform aTransform);

            string aKey = aTransform.mFrame == -1 ? "" : aTransform.mText;
            if (!cachedAttacherInfo.TryGetValue(aKey, out AttacherInfo aAttacherInfo))
            {
                ParseAttacherTrack(aTransform, out aAttacherInfo);
                cachedAttacherInfo.Add(aKey, aAttacherInfo);
            }

            string aReanimationType = GetAttacherReanimationType(aAttacherInfo);
            if (string.IsNullOrEmpty(aReanimationType))  // 如果没有设定当前附属动画名称，或未找到相应的动画
            {
                GlobalMembersAttachment.AttachmentDie(mEffectSystem, ref aTrackInstance.mAttachmentID);  // 清除附件
                return;
            }

            Reanimation aAttachReanim = GlobalMembersAttachment.FindReanimAttachment(mEffectSystem, aTrackInstance.mAttachmentID);
            if (aAttachReanim == null || aAttachReanim.mReanimationType != aReanimationType)  // 如果原先没有附属动画，或原附属动画不是上述设定的动画
            {
                GlobalMembersAttachment.AttachmentDie(mEffectSystem, ref aTrackInstance.mAttachmentID);  // 清除原有附件
                aAttachReanim = mEffectSystem.mReanimationHolder.AllocReanimation(0f, 0f, 0, aReanimationType);  // 重新创建一个指定的动画
                aAttachReanim.mLoopType = aAttacherInfo.mLoopType;
                aAttachReanim.mAnimRate = aAttacherInfo.mAnimRate;
                GlobalMembersAttachment.AttachReanim(mEffectSystem, ref aTrackInstance.mAttachmentID, aAttachReanim, 0f, 0f);
                mFrameBasePose = ReanimatorXnaHelpers.NO_BASE_POSE;  // 设定附属动画后，自身不再存在基准帧
            }

            if (aAttacherInfo.mTrackName.Length != 0)  // 如果定义了附属动画的动作轨道
            {
                aAttachReanim.GetFramesForLayer(aAttacherInfo.mTrackName, out int aAnimFrameStart, out int aAnimFrameCount);
                if (aAttachReanim.mFrameStart != aAnimFrameStart ||
                    aAttachReanim.mFrameCount != aAnimFrameCount)
                {
                    aAttachReanim.StartBlend(20);
                    aAttachReanim.SetFramesForLayer(aAttacherInfo.mTrackName);  // 播放指定轨道上的动作
                }

                if (aAttacherInfo.mAnimRate == 12f &&
                    string.Compare(aAttacherInfo.mTrackName, "anim_walk", StringComparison.InvariantCulture) == 0 &&
                    aAttachReanim.TrackExists("_ground"))
                {
                    AttacherSynchWalkSpeed(theTrackIndex, ref aAttachReanim, ref aAttacherInfo);
                }
                else
                {
                    aAttachReanim.mAnimRate = aAttacherInfo.mAnimRate;
                }

                aAttachReanim.mLoopType = aAttacherInfo.mLoopType;
            }

            SexyColor aColor = TodCommon.ColorsMultiply(mColorOverride, aTrackInstance.mTrackColor);
            aColor.mAlpha = TodCommon.ClampInt(TodCommon.FloatRoundToInt(aTransform.mAlpha * aColor.mAlpha), 0, 255);
            GlobalMembersAttachment.AttachmentPropogateColor(mEffectSystem, aTrackInstance.mAttachmentID, aColor, mEnableExtraAdditiveDraw, mExtraAdditiveColor, mEnableExtraOverlayDraw, mExtraOverlayColor);
        }

        private static string GetAttacherReanimationType(AttacherInfo theAttacherInfo)
        {
            if (theAttacherInfo.mReanimName.Length == 0 ||
                ReanimatorXnaHelpers.gReanimationParamArray is null)
            {
                return null;
            }

            string aReanimFileName = $"reanim/{theAttacherInfo.mReanimName}";
            foreach ((_, ReanimationParams aParams) in ReanimatorXnaHelpers.gReanimationParamArray)
            {
                if (string.Compare(aReanimFileName, aParams.mReanimFileName, StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return aParams.mReanimationType;
                }
            }

            return null;
        }

        public static void ParseAttacherTrack(in ReanimatorTransform theTransform, out AttacherInfo theAttacherInfo)
        {
            theAttacherInfo = new AttacherInfo();
            theAttacherInfo.mReanimName = "";
            theAttacherInfo.mTrackName = "";
            theAttacherInfo.mAnimRate = 12f;
            theAttacherInfo.mLoopType = ReanimLoopType.Loop;
            if (theTransform.mFrame == -1f)  // 如果是空白帧
            {
                return;
            }

            static int IndexOf(ReadOnlySpan<char> chr, string str, int start = 0)
            {
                int aIndex = chr[start..].IndexOf(str);
                if (aIndex == -1)
                {
                    return -1;
                }
                return aIndex + start;
            }

            /* 附属轨道名称格式：attacher__REANIMNAME__TRACKNAME[TAG1][TAG2]…… */
            ReadOnlySpan<char> aText = theTransform.mText;
            int aReanimNameIndex = IndexOf(aText, "__");
            if (aReanimNameIndex == -1)  // 如果字符串中不含双下划线
            {
                return;
            }

            int aTagsIndex = IndexOf(aText, "[", aReanimNameIndex + 2);  // 动画名称之后，指向 TAG 前的中括号
            int aTrackNameIndex = IndexOf(aText, "__", aReanimNameIndex + 2);  // 动画名称之后，指向轨道名称前的双下划线
            if (aTagsIndex != -1 && aTrackNameIndex != -1 && aTagsIndex < aTrackNameIndex)  // 如果“[”之后还有双下划线，则字符串非法
            {
                return;
            }

            if (aTrackNameIndex != -1)  // 如果有定义轨道名称
            {
                theAttacherInfo.mReanimName = theTransform.mText.Substring(aReanimNameIndex + 2, aTrackNameIndex - aReanimNameIndex - 2);  // 取两处双下划线之间的部分（REANIMNAME）
                if (aTagsIndex != -1)  // 如果有定义标签
                {
                    theAttacherInfo.mTrackName = theTransform.mText.Substring(aTrackNameIndex + 2, aTagsIndex - aTrackNameIndex - 2);  // 取到 TAG 的中括号之前
                }
                else
                {
                    theAttacherInfo.mTrackName = theTransform.mText[(aTrackNameIndex + 2)..];  // 取到字符串结尾
                }
            }
            else if (aTagsIndex != -1)  // 如果未定义轨道名称但定义了标签
            {
                theAttacherInfo.mReanimName = theTransform.mText.Substring(aReanimNameIndex + 2, aTagsIndex - aReanimNameIndex - 2);  // 取双下划线至中括号之间的部分
            }
            else  // 如果只定义了轨道名称
            {
                theAttacherInfo.mReanimName = theTransform.mText[(aReanimNameIndex + 2)..];  // 从双下划线之后取到字符串结尾
            }

            while (aTagsIndex != -1)  // 读取每个 TAG
            {
                int aTagEndsIndex = IndexOf(aText, "]", aTagsIndex + 1);
                if (aTagEndsIndex == -1)  // 如果没有右中括号
                {
                    return;
                }

                string aCode = theTransform.mText.Substring(aTagsIndex + 1, aTagEndsIndex - aTagsIndex - 1);  // 取中括号内的文本
                if (float.TryParse(aCode, CultureInfo.InvariantCulture, out float anAnimRate))  // 尝试将文本作为浮点数扫描，如果扫描成功则将结果作为动画速率
                {
                    theAttacherInfo.mAnimRate = anAnimRate;
                }
                else if (string.Compare(aCode, "hold", StringComparison.InvariantCulture) == 0)
                {
                    theAttacherInfo.mLoopType = ReanimLoopType.PlayOnceAndHold;
                }
                else if (string.Compare(aCode, "once", StringComparison.InvariantCulture) == 0)
                {
                    theAttacherInfo.mLoopType = ReanimLoopType.PlayOnce;
                }

                aTagsIndex = IndexOf(aText, "[", aTagEndsIndex + 1);  // 继续寻找下一个 TAG 的左中括号
            }
        }

        public void AttacherSynchWalkSpeed(int theTrackIndex, ref Reanimation theAttachReanim, ref AttacherInfo theAttacherInfo)
        {
            ReanimatorTrack aTrack = mDefinition.mTracks[theTrackIndex];
            GetFrameTime(out ReanimatorFrameTime aFrameTime);
            int aPlaceHolderFrameStart = aFrameTime.mAnimFrameBeforeInt;
            while (aPlaceHolderFrameStart > mFrameStart &&
                aTrack.mTransforms[aPlaceHolderFrameStart - 1].mText == aTrack.mTransforms[aPlaceHolderFrameStart].mText)
            {
                aPlaceHolderFrameStart--;  // 取当前所在区间的第一帧
            }

            int aPlaceHolderFrameEnd = aFrameTime.mAnimFrameBeforeInt;
            while (aPlaceHolderFrameEnd < mFrameStart + mFrameCount - 1 &&
                aTrack.mTransforms[aPlaceHolderFrameEnd + 1].mText == aTrack.mTransforms[aPlaceHolderFrameEnd].mText)
            {
                aPlaceHolderFrameEnd++;  // 取当前所在区间的最后一帧
            }

            int aPlaceHolderFrameCount = aPlaceHolderFrameEnd - aPlaceHolderFrameStart;
            ref ReanimatorTransform aPlaceHolderStartTrans = ref aTrack.mTransforms[aPlaceHolderFrameStart];
            ref ReanimatorTransform aPlaceHolderEndTrans = ref aTrack.mTransforms[aPlaceHolderFrameEnd - 1];
            if (TodCommon.FloatApproxEqual(mAnimRate, 0f))  // 如果动画自身的速率为 0
            {
                theAttachReanim.mAnimRate = 0f;  // 附属动画的速率也为 0
                return;
            }

            float aPlaceHolderDistance = -(aPlaceHolderEndTrans.mTransX - aPlaceHolderStartTrans.mTransX);  // 占位轨道在当前区间内的位移
            float aPlaceHolderSeconds = aPlaceHolderFrameCount / mAnimRate;  // 占位轨道在当前区间内的时长
            if (TodCommon.FloatApproxEqual(aPlaceHolderSeconds, 0f))  // 如果当前所在区间不存在任何帧
            {
                theAttachReanim.mAnimRate = 0f;  // 附属动画的速率为 0
                return;
            }

            int aGroundTrackIndex = theAttachReanim.FindTrackIndex("_ground");
            ReanimatorTrack aGroundTrack = theAttachReanim.mDefinition.mTracks[aGroundTrackIndex];
            ref ReanimatorTransform aTransformGuyStart = ref aGroundTrack.mTransforms[theAttachReanim.mFrameStart];
            ref ReanimatorTransform aTransformGuyEnd = ref aGroundTrack.mTransforms[theAttachReanim.mFrameStart + theAttachReanim.mFrameCount - 1];
            float aGuyDistance = aTransformGuyEnd.mTransX - aTransformGuyStart.mTransX;  // 实际动画在完整动作周期内的位移
            if (aGuyDistance < ReanimatorXnaHelpers.EPSILON || aPlaceHolderDistance < ReanimatorXnaHelpers.EPSILON)  // 如果占位位移为 0 或实际动画周期位移为 0，则附属动画无法移动
            {
                theAttachReanim.mAnimRate = 0f;  // 附属动画的速率为 0
                return;
            }

            float aLoops = aPlaceHolderDistance / aGuyDistance;  // 以附属动画目标位移（占位位移）除以其周期位移，得到附属动画需要循环的周期数
            theAttachReanim.GetCurrentTransform(aGroundTrackIndex, out ReanimatorTransform aTransformGuyCurrent);
            ref ReanimatorTrackInstance reanimatorTrackInstance = ref mTrackInstances[theTrackIndex];
            ref AttachEffect aAttachEffect = ref GlobalMembersAttachment.FindFirstAttachment(mEffectSystem, reanimatorTrackInstance.mAttachmentID);
            if (!Unsafe.IsNullRef(ref aAttachEffect))
            {
                float aGuyCurrentDistance = aTransformGuyCurrent.mTransX - aTransformGuyStart.mTransX;  // 附属动画在其周期内当前已经过的位移
                float aGuyExpectedDistance = aGuyDistance * theAttachReanim.mAnimTime;  // 以匀速运动的占位轨道计算的、附属动画当前的理论位移
                aAttachEffect.mOffset.M41 = aGuyExpectedDistance - aGuyCurrentDistance;  // 调整附属效果的横向变换以使附属动画的位移保持与占位动画一致
            }

            theAttachReanim.mAnimRate = aLoops * theAttachReanim.mFrameCount / aPlaceHolderSeconds;  // 速率 = 需要播放的帧数 ÷ 可以播放的时长
        }

        public bool IsAnimPlaying(string theTrackName)
        {
            GetFramesForLayer(theTrackName, out int aFrameStart, out int aFrameCount);
            return mFrameStart == aFrameStart && mFrameCount == aFrameCount;
        }

        public void SetBasePoseFromAnim(string theTrackName)
        {
            GetFramesForLayer(theTrackName, out int aFrameStart, out int aFrameCount);
            mFrameBasePose = aFrameStart;  // 将当前轨道动画的起始帧作为变换基准帧
        }

        public void ReanimBltMatrix(EffectViewer.TodLib.Graphics.Graphics g, Image theImage, in Matrix4x4 theTransform, in Rectangle theClipRect, in SexyColor theColor, DrawMode theDrawMode, in Rectangle theSrcRect)
        {
            TodCommon.TodBltMatrix(g, theImage, theTransform, theClipRect, theColor, theDrawMode, theSrcRect);
        }

        public Reanimation FindSubReanim(string theReanimType)
        {
            if (mReanimationType == theReanimType)
            {
                return this;
            }

            for (int i = 0; i < mDefinition.mTrackCount; i++)
            {
                Reanimation aReanimation = GlobalMembersAttachment.FindReanimAttachment(mEffectSystem, mTrackInstances[i].mAttachmentID);
                if (aReanimation != null)
                {
                    Reanimation aSubReanim = aReanimation.FindSubReanim(theReanimType);
                    if (aSubReanim != null)
                    {
                        return aSubReanim;
                    }
                }
            }

            return null;
        }
    }
}
