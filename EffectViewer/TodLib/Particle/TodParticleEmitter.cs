using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace EffectViewer.TodLib.Particle
{
    public class TodParticleEmitter : IDataArrayItem
    {
        public TodEmitterDefinition mEmitterDef;
        public TodParticleSystem mParticleSystem;
        public readonly LinkedList<ParticleID> mParticleList = [];
        public float mSpawnAccum;
        public Vector2 mSystemCenter;
        public int mParticlesSpawned;
        public int mSystemAge;
        public int mSystemDuration;
        public float mSystemTimeValue;
        public float mSystemLastTimeValue;
        public bool mDead;
        public SexyColor mColorOverride;
        public bool mExtraAdditiveDrawOverride;
        public float mScaleOverride;
        public Image mImageOverride;
        public ParticleEmitterID mCrossFadeEmitterID;
        public int mEmitterCrossFadeCountDown;
        public int mFrameOverride;
        public InlineArray10<float> mTrackInterp; // ParticleSystemTracks.NumSystemTracks
        public InlineArray4<InlineArray2<float>> mSystemFieldInterp; // TodLibConstants.MAX_PARTICLE_FIELDS

        uint IDataArrayItem.Id { get; set; }
        int IDataArrayItem.Index { get; init; }

        public TodParticleEmitter()
        {
            Reset();
        }

        public void Reset()
        {
            mEmitterDef = null;
            mParticleSystem = null;
            mParticleList.Clear();
            mSpawnAccum = 0f;
            mSystemCenter = default;
            mParticlesSpawned = 0;
            mSystemAge = 0;
            mSystemDuration = 0;
            mSystemTimeValue = 0f;
            mSystemLastTimeValue = 0f;
            mDead = false;
            mColorOverride = default;
            mExtraAdditiveDrawOverride = false;
            mScaleOverride = 0f;
            mImageOverride = null;
            mCrossFadeEmitterID = ParticleEmitterID.Null;
            mEmitterCrossFadeCountDown = 0;
            mFrameOverride = 0;
            foreach (ref float item in mTrackInterp)
            {
                item = 0f;
            }
            foreach (ref InlineArray2<float> item in mSystemFieldInterp)
            {
                item[0] = 0f;
                item[1] = 0f;
            }
        }

        public void Dispose()
        {

        }

        public void TodEmitterInitialize(float theX, float theY, TodParticleSystem theSystem, TodEmitterDefinition theEmitterDef)
        {
            mSpawnAccum = 0f;
            mParticlesSpawned = 0;
            mSystemTimeValue = -1f;
            mSystemLastTimeValue = -1f;
            mSystemAge = -1;
            mDead = false;
            mColorOverride = SexyColor.White;
            mSystemCenter.X = theX;
            mSystemCenter.Y = theY;
            mFrameOverride = -1;
            mParticleSystem = theSystem;
            mScaleOverride = 1f;
            mExtraAdditiveDrawOverride = false;
            mImageOverride = null;
            mSystemDuration = 0;
            mEmitterDef = theEmitterDef;
            
            if (Definition.FloatTrackIsSet(mEmitterDef.mSystemDuration))
            {
                mSystemDuration = (int)Definition.FloatTrackEvaluate(mEmitterDef.mSystemDuration, 0f, RandomNumbers.Rand(1f));
            }
            else
            {
                mSystemDuration = (int)Definition.FloatTrackEvaluate(mEmitterDef.mParticleDuration, 0f, 1f);
            }
            mSystemDuration = Math.Max(1, mSystemDuration);

            for (int i = 0; i < mEmitterDef.mSystemFieldCount; i++)
            {
                mSystemFieldInterp[i][0] = RandomNumbers.Rand(1f);
                mSystemFieldInterp[i][1] = RandomNumbers.Rand(1f);
            }
            for (int j = 0; j < (int)ParticleSystemTracks.NumSystemTracks; j++)
            {
                mTrackInterp[j] = RandomNumbers.Rand(1f);
            }

            Update();
        }

        public void Update()
        {
            if (mDead)
            {
                return;
            }

            mSystemAge++;
            bool aDie = false;
            if (mSystemAge >= mSystemDuration)  // 发射器的生命周期结束时
            {
                if (TodCommon.TestBit((uint)mEmitterDef.mParticleFlags, (int)ParticleFlags.SystemLoops))  // 判断发射器是否循环
                {
                    mSystemAge = 0;  // 重置发射器当前帧
                }
                else
                {
                    mSystemAge = mSystemDuration - 1;  // 将发射器滞留在最后一帧
                    aDie = true;
                }
            }

            if (mEmitterCrossFadeCountDown > 0)
            {
                mEmitterCrossFadeCountDown--;  // 更新发射器的交叉混合
                if (mEmitterCrossFadeCountDown == 0)
                {
                    aDie = true;
                }
            }

            if (mCrossFadeEmitterID != ParticleEmitterID.Null)
            {
                TodParticleEmitter aCrossFadeEmitter = mParticleSystem.mParticleHolder.mEmitters.DataArrayTryToGet(mCrossFadeEmitterID);
                if (aCrossFadeEmitter == null || aCrossFadeEmitter.mDead)
                {
                    aDie = true;
                }
            }

            mSystemTimeValue = mSystemAge / (float)(mSystemDuration - 1);
            for (int i = 0; i < mEmitterDef.mSystemFieldCount; i++)
            {
                UpdateSystemField(mEmitterDef.mSystemFields[i], mSystemTimeValue, i);  // 更新发射器受到每个系统场的作用
            }
            for (LinkedListNode<ParticleID> aNode = mParticleList.First; aNode != null;)
            {
                TodParticle aParticle = mParticleSystem.mParticleHolder.mParticles.DataArrayGet(aNode.Value);
                aNode = aNode.Next;
                if (!UpdateParticle(aParticle))  // 更新发射器中的每个粒子
                {
                    DeleteParticle(aParticle);
                }
            }

            UpdateSpawning();  // 更新粒子发射

            if (aDie)
            {
                DeleteNonCrossFading();
                if (mParticleList.Count == 0)
                {
                    mDead = true;
                    return;
                }
            }

            mSystemLastTimeValue = mSystemTimeValue;
        }

        public void Draw(EffectViewer.TodLib.Graphics.Graphics g)
        {
            bool aHardWare = true; // 锁定开启3D加速
            if (TodCommon.TestBit((uint)mEmitterDef.mParticleFlags, (int)ParticleFlags.SoftwareOnly) &&
                aHardWare)
            {
                return;
            }

            if (TodCommon.TestBit((uint)mEmitterDef.mParticleFlags, (int)ParticleFlags.HardwareOnly) &&
                !aHardWare)
            {
                return;
            }

            for (LinkedListNode<ParticleID> aNode = mParticleList.First; aNode != null; aNode = aNode.Next)
            {
                DrawParticle(g, mParticleSystem.mParticleHolder.mParticles.DataArrayGet(aNode.Value));
            }
        }

        public void SystemMove(float theX, float theY)
        {
            float aDeltaX = theX - mSystemCenter.X;
            float aDeltaY = theY - mSystemCenter.Y;
            if (TodCommon.FloatApproxEqual(aDeltaX, 0f) &&
                TodCommon.FloatApproxEqual(aDeltaY, 0f))  // 移动前后的坐标几乎相等时，无需操作
            {
                return;
            }

            mSystemCenter.X = theX;
            mSystemCenter.Y = theY;
            if (!TodCommon.TestBit((uint)mEmitterDef.mParticleFlags, (int)ParticleFlags.ParticlesDontFollow))
            {
                for (LinkedListNode<ParticleID> aNode = mParticleList.First; aNode != null; aNode = aNode.Next)
                {
                    TodParticle aParticle = mParticleSystem.mParticleHolder.mParticles.DataArrayGet(aNode.Value);
                    aParticle.mPosition.X += aDeltaX;
                    aParticle.mPosition.Y += aDeltaY;
                }
            }
        }

        public static bool GetRenderParams(TodParticle theParticle, ref ParticleRenderParams theParams)
        {
            TodParticleEmitter aEmitter = theParticle.mParticleEmitter;
            TodEmitterDefinition aDef = aEmitter.mEmitterDef;

            // 颜色。对于每一色彩通道，当系统对应轨道、粒子对应轨道和对应覆写中任一有定义时，认为该通道已设定。
            theParams.mRedIsSet = false;
            theParams.mRedIsSet |= Definition.FloatTrackIsSet(aDef.mSystemRed);
            theParams.mRedIsSet |= Definition.FloatTrackIsSet(aDef.mParticleRed);
            theParams.mRedIsSet |= aEmitter.mColorOverride.mRed != 1f;
            theParams.mGreenIsSet = false;
            theParams.mGreenIsSet |= Definition.FloatTrackIsSet(aDef.mSystemGreen);
            theParams.mGreenIsSet |= Definition.FloatTrackIsSet(aDef.mParticleGreen);
            theParams.mGreenIsSet |= aEmitter.mColorOverride.mGreen != 1f;
            theParams.mBlueIsSet = false;
            theParams.mBlueIsSet |= Definition.FloatTrackIsSet(aDef.mSystemBlue);
            theParams.mBlueIsSet |= Definition.FloatTrackIsSet(aDef.mParticleBlue);
            theParams.mBlueIsSet |= aEmitter.mColorOverride.mBlue != 1f;
            theParams.mAlphaIsSet = false;
            theParams.mAlphaIsSet |= Definition.FloatTrackIsSet(aDef.mSystemAlpha);
            theParams.mAlphaIsSet |= Definition.FloatTrackIsSet(aDef.mParticleAlpha);
            theParams.mAlphaIsSet |= aEmitter.mColorOverride.mAlpha != 1f;
            // 缩放
            theParams.mParticleScaleIsSet = false;
            theParams.mParticleScaleIsSet |= Definition.FloatTrackIsSet(aDef.mParticleScale);
            theParams.mParticleScaleIsSet |= aEmitter.mScaleOverride != 1f;
            // 拉伸
            theParams.mParticleStretchIsSet = Definition.FloatTrackIsSet(aDef.mParticleStretch);
            // 旋转角度。当粒子使用随机初始旋转角度或对齐发射方向角度时，也可认为旋转角度已设定。
            theParams.mSpinPositionIsSet = false;
            theParams.mSpinPositionIsSet |= Definition.FloatTrackIsSet(aDef.mParticleSpinSpeed);
            theParams.mSpinPositionIsSet |= Definition.FloatTrackIsSet(aDef.mParticleSpinAngle);
            theParams.mSpinPositionIsSet |= TodCommon.TestBit((uint)aDef.mParticleFlags, (int)ParticleFlags.RandomLaunchSpin);
            theParams.mSpinPositionIsSet |= TodCommon.TestBit((uint)aDef.mParticleFlags, (int)ParticleFlags.AlignLaunchSpin);
            // 位置
            theParams.mPositionIsSet = false;
            theParams.mPositionIsSet |= aDef.mParticleFieldCount > 0f;
            theParams.mPositionIsSet |= Definition.FloatTrackIsSet(aDef.mEmitterRadius);
            theParams.mPositionIsSet |= Definition.FloatTrackIsSet(aDef.mEmitterOffsetX);
            theParams.mPositionIsSet |= Definition.FloatTrackIsSet(aDef.mEmitterOffsetY);
            theParams.mPositionIsSet |= Definition.FloatTrackIsSet(aDef.mEmitterBoxX);
            theParams.mPositionIsSet |= Definition.FloatTrackIsSet(aDef.mEmitterBoxY);

            float aSystemRed = aEmitter.SystemTrackEvaluate(aDef.mSystemRed, ParticleSystemTracks.SystemRed);
            float aSystemGreen = aEmitter.SystemTrackEvaluate(aDef.mSystemGreen, ParticleSystemTracks.SystemGreen);
            float aSystemBlue = aEmitter.SystemTrackEvaluate(aDef.mSystemBlue, ParticleSystemTracks.SystemBlue);
            float aSystemAlpha = aEmitter.SystemTrackEvaluate(aDef.mSystemAlpha, ParticleSystemTracks.SystemAlpha);
            float aSystemBrightness = aEmitter.SystemTrackEvaluate(aDef.mSystemBrightness, ParticleSystemTracks.SystemBrightness);
            float aParticleRed = ParticleTrackEvaluate(aDef.mParticleRed, theParticle, ParticleTracks.ParticleRed);
            float aParticleGreen = ParticleTrackEvaluate(aDef.mParticleGreen, theParticle, ParticleTracks.ParticleGreen);
            float aParticleBlue = ParticleTrackEvaluate(aDef.mParticleBlue, theParticle, ParticleTracks.ParticleBlue);
            float aParticleAlpha = ParticleTrackEvaluate(aDef.mParticleAlpha, theParticle, ParticleTracks.ParticleAlpha);
            float aParticleBrightness = ParticleTrackEvaluate(aDef.mParticleBrightness, theParticle, ParticleTracks.ParticleBrightness);
            float aBrightness = aParticleBrightness * aSystemBrightness;
            // 实际颜色 = 粒子颜色 * 系统颜色 * 覆写颜色 * 亮度
            theParams.mRed = aParticleRed * aSystemRed * aEmitter.mColorOverride.mRed * aBrightness;
            theParams.mGreen = aParticleGreen * aSystemGreen * aEmitter.mColorOverride.mGreen * aBrightness;
            theParams.mBlue = aParticleBlue * aSystemBlue * aEmitter.mColorOverride.mBlue * aBrightness;
            theParams.mAlpha = aParticleAlpha * aSystemAlpha * aEmitter.mColorOverride.mAlpha;
            theParams.mPosX = theParticle.mPosition.X;
            theParams.mPosY = theParticle.mPosition.Y;
            float aParticleScale = ParticleTrackEvaluate(aDef.mParticleScale, theParticle, ParticleTracks.ParticleScale);
            theParams.mParticleStretch = ParticleTrackEvaluate(aDef.mParticleStretch, theParticle, ParticleTracks.ParticleStretch);
            theParams.mParticleScale = aParticleScale * aEmitter.mScaleOverride;
            theParams.mSpinPosition = theParticle.mSpinPosition;

            TodParticle aCrossFadeParticle = aEmitter.mParticleSystem.mParticleHolder.mParticles.DataArrayTryToGet(theParticle.mCrossFadeParticleID);
            if (aCrossFadeParticle != null)  // 当存在交叉混合的粒子时，将二者的渲染参数进行混合（从 aCrossFadeParticle 至 theParticle 的交叉混合）
            {
                ParticleRenderParams aCrossFadeParams = default;
                if (GetRenderParams(aCrossFadeParticle, ref aCrossFadeParams))
                {
                    float aFraction = theParticle.mParticleAge / (float)(aCrossFadeParticle.mCrossFadeDuration - 1);
                    // 各项数值按照 aFraction 比例混合
                    theParams.mRed = TodParticleGlobal.CrossFadeLerp(aCrossFadeParams.mRed, theParams.mRed, aCrossFadeParams.mRedIsSet, theParams.mRedIsSet, aFraction);
                    theParams.mGreen = TodParticleGlobal.CrossFadeLerp(aCrossFadeParams.mGreen, theParams.mGreen, aCrossFadeParams.mGreenIsSet, theParams.mGreenIsSet, aFraction);
                    theParams.mBlue = TodParticleGlobal.CrossFadeLerp(aCrossFadeParams.mBlue, theParams.mBlue, aCrossFadeParams.mBlueIsSet, theParams.mBlueIsSet, aFraction);
                    theParams.mAlpha = TodParticleGlobal.CrossFadeLerp(aCrossFadeParams.mAlpha, theParams.mAlpha, aCrossFadeParams.mAlphaIsSet, theParams.mAlphaIsSet, aFraction);
                    theParams.mParticleScale = TodParticleGlobal.CrossFadeLerp(aCrossFadeParams.mParticleScale, theParams.mParticleScale, aCrossFadeParams.mParticleScaleIsSet, theParams.mParticleScaleIsSet, aFraction);
                    theParams.mParticleStretch = TodParticleGlobal.CrossFadeLerp(aCrossFadeParams.mParticleStretch, theParams.mParticleStretch, aCrossFadeParams.mParticleStretchIsSet, theParams.mParticleStretchIsSet, aFraction);
                    theParams.mSpinPosition = TodParticleGlobal.CrossFadeLerp(aCrossFadeParams.mSpinPosition, theParams.mSpinPosition, aCrossFadeParams.mSpinPositionIsSet, theParams.mSpinPositionIsSet, aFraction);
                    theParams.mPosX = TodParticleGlobal.CrossFadeLerp(aCrossFadeParams.mPosX, theParams.mPosX, aCrossFadeParams.mPositionIsSet, theParams.mPositionIsSet, aFraction);
                    theParams.mPosY = TodParticleGlobal.CrossFadeLerp(aCrossFadeParams.mPosY, theParams.mPosY, aCrossFadeParams.mPositionIsSet, theParams.mPositionIsSet, aFraction);
                    // 当交叉混合来源的某项已设定时，可以认为该粒子的对应项也已设定
                    theParams.mRedIsSet |= aCrossFadeParams.mRedIsSet;
                    theParams.mGreenIsSet |= aCrossFadeParams.mGreenIsSet;
                    theParams.mBlueIsSet |= aCrossFadeParams.mBlueIsSet;
                    theParams.mAlphaIsSet |= aCrossFadeParams.mAlphaIsSet;
                    theParams.mParticleScaleIsSet |= aCrossFadeParams.mParticleScaleIsSet;
                    theParams.mParticleStretchIsSet |= aCrossFadeParams.mParticleStretchIsSet;
                    theParams.mSpinPositionIsSet |= aCrossFadeParams.mSpinPositionIsSet;
                    theParams.mPositionIsSet |= aCrossFadeParams.mPositionIsSet;
                }
            }

            return true;
        }

        public void DrawParticle(EffectViewer.TodLib.Graphics.Graphics g, TodParticle theParticle)
        {
            if (theParticle.mCrossFadeDuration > 0)  // 交叉混合的源粒子，不绘制
            {
                return;
            }

            ParticleRenderParams aParams = default;
            if (GetRenderParams(theParticle, ref aParams))
            {
                SexyColor aColor = new(
                    TodCommon.ClampInt(TodCommon.FloatRoundToInt(aParams.mRed), 0, 255),
                    TodCommon.ClampInt(TodCommon.FloatRoundToInt(aParams.mGreen), 0, 255),
                    TodCommon.ClampInt(TodCommon.FloatRoundToInt(aParams.mBlue), 0, 255),
                    TodCommon.ClampInt(TodCommon.FloatRoundToInt(aParams.mAlpha), 0, 255)
                    );
                if (aColor.mAlpha > 0)  // 不透明度为 0 时，不绘制
                {
                    aParams.mPosX += g.mTransX;
                    aParams.mPosY += g.mTransY;

                    TodParticle aParticle;
                    if (mImageOverride != null || mEmitterDef.mImage != null)  // 粒子有贴图时，渲染该粒子
                    {
                        aParticle = theParticle;
                    }
                    else
                    {
                        aParticle = mParticleSystem.mParticleHolder.mParticles.DataArrayTryToGet(theParticle.mCrossFadeParticleID);
                    }

                    if (aParticle != null)
                    {
                        TodParticleGlobal.RenderParticle(g, aParticle, aColor, ref aParams);
                    }
                }
            }
        }

        public void UpdateSpawning()
        {
            TodParticleEmitter aCrossFadeEmitter = mParticleSystem.mParticleHolder.mEmitters.DataArrayTryToGet(mCrossFadeEmitterID);
            TodParticleEmitter aSpawningEmitter = aCrossFadeEmitter ?? this;  // 各项数据的计算均以此“主发射器”为准

            mSpawnAccum += aSpawningEmitter.SystemTrackEvaluate(aSpawningEmitter.mEmitterDef.mSpawnRate, ParticleSystemTracks.SpawnRate) * 0.01f;
            int aSpawnCount = (int)mSpawnAccum;
            mSpawnAccum -= aSpawnCount;

            int aSpawnMinActive = (int)aSpawningEmitter.SystemTrackEvaluate(aSpawningEmitter.mEmitterDef.mSpawnMinActive, ParticleSystemTracks.SpawnMinActive);
            if (aSpawnMinActive >= 0 &&
                aSpawnCount < aSpawnMinActive - mParticleList.Count)
            {
                aSpawnCount = aSpawnMinActive - mParticleList.Count;  // 至少确保将粒子数量增加至 aSpawnMinActive 个
            }

            int aSpawnMaxActive = (int)aSpawningEmitter.SystemTrackEvaluate(aSpawningEmitter.mEmitterDef.mSpawnMaxActive, ParticleSystemTracks.SpawnMaxActive);
            if (aSpawnMaxActive >= 0 &&
                aSpawnCount > aSpawnMaxActive - mParticleList.Count)
            {
                aSpawnCount = aSpawnMaxActive - mParticleList.Count;  // 至多保证粒子数量不会超过 aSpawnMaxActive 个
            }

            if (Definition.FloatTrackIsSet(aSpawningEmitter.mEmitterDef.mSpawnMaxLaunched))
            {
                int aSpawnMaxLaunched = (int)aSpawningEmitter.SystemTrackEvaluate(aSpawningEmitter.mEmitterDef.mSpawnMaxLaunched, ParticleSystemTracks.SpawnMaxLaunched);
                if (aSpawnCount > aSpawnMaxLaunched - mParticlesSpawned)
                {
                    aSpawnCount = aSpawnMaxLaunched - mParticlesSpawned;  // 确保发射数量不超过发射器总共能发射的最大数量
                }
            }

            for (int i = 0; i < aSpawnCount; i++)
            {
                TodParticle aParticle = SpawnParticle(i, aSpawnCount);
                if (aCrossFadeEmitter != null)
                {
                    CrossFadeParticle(aParticle, aCrossFadeEmitter);
                }
            }
        }

        public bool UpdateParticle(TodParticle theParticle)
        {
            if (theParticle.mParticleAge >= theParticle.mParticleDuration)  // 粒子的生命周期结束时
            {
                if (TodCommon.TestBit((uint)mEmitterDef.mParticleFlags, (int)ParticleFlags.ParticleLoops))  // 判断粒子是否循环
                {
                    theParticle.mParticleAge = 0;  // 重置粒子当前帧
                }
                else if (theParticle.mCrossFadeDuration > 0)  // 判断粒子是否处于交叉混合过程中
                {
                    theParticle.mParticleAge = theParticle.mParticleDuration - 1;  // 将粒子滞留在最后一帧
                }
                else if (string.IsNullOrEmpty(mEmitterDef.mOnDuration) ||
                    !CrossFadeParticleToName(theParticle, mEmitterDef.mOnDuration))  // 尝试进行交叉混合
                {
                    return false;
                }
            }

            if (theParticle.mCrossFadeParticleID != ParticleID.Null &&
                mParticleSystem.mParticleHolder.mParticles.DataArrayTryToGet(theParticle.mCrossFadeParticleID) == null)
            {
                return false;  // 当粒子不存在交叉混合时，可以删除粒子
            }

            theParticle.mParticleTimeValue = theParticle.mParticleAge / (float)(theParticle.mParticleDuration - 1);
            for (int i = 0; i < mEmitterDef.mParticleFieldCount; i++)  // 更新粒子受到每个粒子场的作用
            {
                UpdateParticleField(theParticle, mEmitterDef.mParticleFields[i], theParticle.mParticleTimeValue, i);
            }

            theParticle.mPosition += theParticle.mVelocity;
            float aSpinSpeed = ParticleTrackEvaluate(mEmitterDef.mParticleSpinSpeed, theParticle, ParticleTracks.ParticleSpinSpeed) * 0.01f;
            float aSpinAngle = ParticleTrackEvaluate(mEmitterDef.mParticleSpinAngle, theParticle, ParticleTracks.ParticleSpinAngle);
            float aLastSpinAngle = TodParticleGlobal.FloatTrackEvaluateFromLastTime(mEmitterDef.mParticleSpinAngle, theParticle.mParticleLastTimeValue, theParticle.mParticleInterp[(int)ParticleTracks.ParticleSpinAngle]);
            theParticle.mSpinPosition += TodCommon.DegToRad(aSpinSpeed + aSpinAngle - aLastSpinAngle) + theParticle.mSpinVelocity;  // 更新粒子旋转角度

            if (Definition.FloatTrackIsSet(mEmitterDef.mAnimationRate))  // 如果定义了动画速率
            {
                float aAnimTime = ParticleTrackEvaluate(mEmitterDef.mAnimationRate, theParticle, ParticleTracks.ParticleAnimationRate) * 0.01f;
                theParticle.mAnimationTimeValue += aAnimTime;  // 更新动画时间值（动画循环率）
                while (theParticle.mAnimationTimeValue >= 1f)
                {
                    theParticle.mAnimationTimeValue -= 1f;
                }
                while (theParticle.mAnimationTimeValue < 0f)
                {
                    theParticle.mAnimationTimeValue += 1f;
                }
            }

            theParticle.mParticleAge++;
            theParticle.mParticleLastTimeValue = theParticle.mParticleTimeValue;
            return true;
        }

        public TodParticle SpawnParticle(int theIndex, int theSpawnCount)
        {
            DataArray<TodParticle, ParticleID> aDataArray = mParticleSystem.mParticleHolder.mParticles;
            if (aDataArray.mSize == aDataArray.mMaxSize)
            {
                Debug.Log(DebugType.Warn, $"Too many particles '{mEmitterDef.mName}'");
                return null;
            }

            TodParticle aParticle = aDataArray.DataArrayAlloc();
            Debug.ASSERT(mEmitterDef.mParticleFieldCount <= TodLibConstants.MAX_PARTICLE_FIELDS);
            for (int i = 0; i < mEmitterDef.mParticleFieldCount; i++)
            {
                aParticle.mParticleFieldInterp[i][0] = RandomNumbers.Rand(1f);  // 初始化每个粒子场的横向插值
                aParticle.mParticleFieldInterp[i][1] = RandomNumbers.Rand(1f);  // 初始化每个粒子场的纵向插值
            }

            for (int j = 0; j < (int)ParticleTracks.NumParticleTracks; j++)
            {
                aParticle.mParticleInterp[j] = RandomNumbers.Rand(1f);  // 初始化每条通道的插值
            }

            float aParticleDurationInterp = RandomNumbers.Rand(1f);
            float aLaunchSpeedInterp = RandomNumbers.Rand(1f);
            float aEmitterOffsetXInterp = RandomNumbers.Rand(1f);
            float aEmitterOffsetYInterp = RandomNumbers.Rand(1f);
            aParticle.mParticleDuration = (int)Definition.FloatTrackEvaluate(mEmitterDef.mParticleDuration, mSystemTimeValue, aParticleDurationInterp);
            aParticle.mParticleDuration = Math.Max(1, aParticle.mParticleDuration);  // 初始化粒子持续时间（至少为 1）
            aParticle.mParticleAge = 0;
            aParticle.mParticleEmitter = this;
            aParticle.mParticleTimeValue = -1f;
            aParticle.mParticleLastTimeValue = -1f;
            if (TodCommon.TestBit((uint)mEmitterDef.mParticleFlags, (int)ParticleFlags.RandomStartTime))
            {
                aParticle.mParticleAge = RandomNumbers.Rand(aParticle.mParticleDuration);  // 对于“随机初始时间”的粒子
            }

            float aLaunchSpeed = Definition.FloatTrackEvaluate(mEmitterDef.mLaunchSpeed, mSystemTimeValue, aLaunchSpeedInterp) * 0.01f;
            float aLaunchAngleInterp = RandomNumbers.Rand(1f);

            float aLaunchAngle;
            if (mEmitterDef.mEmitterType == EmitterType.CirclePath)
            {
                // 发射角度 = 根据路径定义计算的圆周上的基础角度 + 根据发射角度定义计算的额外偏移的角度
                aLaunchAngle = Definition.FloatTrackEvaluate(mEmitterDef.mEmitterPath, mSystemTimeValue, mTrackInterp[(int)ParticleSystemTracks.EmitterPath]) * MathF.Tau;
                aLaunchAngle += TodCommon.DegToRad(Definition.FloatTrackEvaluate(mEmitterDef.mLaunchAngle, mSystemTimeValue, aLaunchAngleInterp));
            }
            else if (mEmitterDef.mEmitterType == EmitterType.CircleEvenSpacing)
            {
                // 基础发射角度要使 theSpawnCount 个粒子平均布满圆周
                aLaunchAngle = MathF.Tau * theIndex / theSpawnCount + TodCommon.DegToRad(Definition.FloatTrackEvaluate(mEmitterDef.mLaunchAngle, mSystemTimeValue, aLaunchAngleInterp));
            }
            else if (Definition.FloatTrackIsConstantZero(mEmitterDef.mLaunchAngle))
            {
                // 未定义的轨道，发射角度直接取 [0, 2pi] 的随机值
                aLaunchAngle = RandomNumbers.Rand(MathF.Tau);
            }
            else
            {
                aLaunchAngle = TodCommon.DegToRad(Definition.FloatTrackEvaluate(mEmitterDef.mLaunchAngle, mSystemTimeValue, aLaunchAngleInterp));
            }

            float aPosX = 0f;
            float aPosY = 0f;
            if (mEmitterDef.mEmitterType == EmitterType.Circle ||
                mEmitterDef.mEmitterType == EmitterType.CirclePath ||
                mEmitterDef.mEmitterType == EmitterType.CircleEvenSpacing)
            {
                float aEmitterRadiusInterp = RandomNumbers.Rand(1f);
                float aRadius = Definition.FloatTrackEvaluate(mEmitterDef.mEmitterRadius, mSystemTimeValue, aEmitterRadiusInterp);
                // ★ 以竖直向下的方向为 0 角度
                aPosX = MathF.Sin(aLaunchAngle) * aRadius;
                aPosY = MathF.Cos(aLaunchAngle) * aRadius;
            }
            else if (mEmitterDef.mEmitterType == EmitterType.Box)
            {
                float aEmitterBoxXInterp = RandomNumbers.Rand(1f);
                float aEmitterBoxYInterp = RandomNumbers.Rand(1f);
                aPosX = Definition.FloatTrackEvaluate(mEmitterDef.mEmitterBoxX, mSystemTimeValue, aEmitterBoxXInterp);
                aPosY = Definition.FloatTrackEvaluate(mEmitterDef.mEmitterBoxY, mSystemTimeValue, aEmitterBoxYInterp);
            }
            else if (mEmitterDef.mEmitterType == EmitterType.BoxPath)
            {
                float aEmitterPathPosition = Definition.FloatTrackEvaluate(mEmitterDef.mEmitterPath, mSystemTimeValue, mTrackInterp[4]);
                float aMinX = Definition.FloatTrackEvaluate(mEmitterDef.mEmitterBoxX, mSystemTimeValue, 0f);
                float aMaxX = Definition.FloatTrackEvaluate(mEmitterDef.mEmitterBoxX, mSystemTimeValue, 1f);
                float aMinY = Definition.FloatTrackEvaluate(mEmitterDef.mEmitterBoxY, mSystemTimeValue, 0f);
                float aMaxY = Definition.FloatTrackEvaluate(mEmitterDef.mEmitterBoxY, mSystemTimeValue, 1f);
                float aDistanceX = aMaxX - aMinX;  // 路径矩形的横向宽度
                float aDistanceY = aMaxY - aMinY;  // 路径矩形的纵向高度
                float aPathPos = aEmitterPathPosition * (aDistanceY + aDistanceX + aDistanceY + aDistanceX);  // 发射点位于矩形边上的位置
                // ★ 注释规定：以矩形左上的顶点开始，按逆时针方向依次将矩形的四个顶点标记为 A、B、C、D，并标记发射点为 P
		        // ★           如此，aPathPos 即为 P 点与 A 点在矩形路径上的距离。注意，游戏中取横向水平向右和纵向竖直向下为正方向。
                if (aPathPos < aDistanceY)  // 发射点落在矩形 AB 边（左边）上
                {
                    aPosX = aMinX;  // 横坐标 = 矩形左端横坐标
                    aPosY = aMinY + aPathPos; // ((aMaxY - aMinY) * (aPathPos / aDistanceY));  // 纵坐标 = 矩形底端坐标 + |PA|，此处乘除相当于没算
                }
                else if (aPathPos < aDistanceY + aDistanceX)
                {
                    aPosX = aMinX + (aPathPos - aDistanceY); // ((aMaxX - aMinX) * ((aPathPos - aDistanceY) / aDistanceX));  // 横坐标 = 矩形左端横坐标 + |PB|，此处乘除相当于没算
                    aPosY = aMaxY;
                }
                else if (aPathPos < aDistanceY + aDistanceX + aDistanceY)  // 发射点落在矩形 CD 边（右边）上
                {
                    aPosX = aMaxX;
                    aPosY = aMaxY + -(aPathPos - aDistanceY - aDistanceX); // ((aMinY - aMaxY) * ((aPathPos - aDistanceY - aDistanceX) / aDistanceY));  // 纵坐标 = 矩形顶端纵坐标 - |PC|，此处乘除相当于没算
                }
                else  // 发射点落在矩形 AD 边（底边）上
                {
                    aPosX = aMaxX + -(aPathPos - aDistanceY - aDistanceX - aDistanceY); // ((aMinX - aMaxX) * ((aPathPos - aDistanceY - aDistanceX - aDistanceY) / aDistanceX));  // 横坐标 = 矩形右端横坐标 - |PD|，此处乘除相当于没算
                    aPosY = aMinY;
                }
            }
            else
            {
                Debug.ASSERT(false);
            }

            float aEmitterSkewXInterp = RandomNumbers.Rand(1f);
            float aEmitterSkewYInterp = RandomNumbers.Rand(1f);
            float aSkewX = Definition.FloatTrackEvaluate(mEmitterDef.mEmitterSkewX, mSystemTimeValue, aEmitterSkewXInterp);
            float aSkewY = Definition.FloatTrackEvaluate(mEmitterDef.mEmitterSkewY, mSystemTimeValue, aEmitterSkewYInterp);
            aParticle.mPosition.X = mSystemCenter.X + aPosX + (aPosY * aSkewX);  // 横向（左右）倾斜的幅度受纵坐标影响
            aParticle.mPosition.Y = mSystemCenter.Y + aPosY + (aPosX * aSkewY);  // 纵向（上下）倾斜的幅度受横坐标影响
            aParticle.mVelocity.X = MathF.Sin(aLaunchAngle) * aLaunchSpeed;
            aParticle.mVelocity.Y = MathF.Cos(aLaunchAngle) * aLaunchSpeed;
            aParticle.mPosition.X += Definition.FloatTrackEvaluate(mEmitterDef.mEmitterOffsetX, mSystemTimeValue, aEmitterOffsetXInterp);  // 横坐标加上横向偏移值
            aParticle.mPosition.Y += Definition.FloatTrackEvaluate(mEmitterDef.mEmitterOffsetY, mSystemTimeValue, aEmitterOffsetYInterp);  // 纵坐标加上纵向偏移值

            aParticle.mAnimationTimeValue = 0f;
            if (mEmitterDef.mAnimated != 0 ||
                Definition.FloatTrackIsSet(mEmitterDef.mAnimationRate))
            {
                aParticle.mImageFrame = 0;  // 如果定义了粒子动态或动画速率，则粒子的当前帧将在后续根据粒子时间值或动画循环率实时计算，此处先初始化为 0
            }
            else
            {
                aParticle.mImageFrame = RandomNumbers.Rand(mEmitterDef.mImageFrames);  // 对于帧固定的粒子，在贴图的所有帧中随机取得一帧，后续一般不再变化
            }

            if (TodCommon.TestBit((uint)mEmitterDef.mParticleFlags, (int)ParticleFlags.RandomLaunchSpin))
            {
                aParticle.mSpinPosition = RandomNumbers.Rand(MathF.Tau);  // 在 [0, 2pi] 之间随机取得一个初始旋转角度
            }
            else if (TodCommon.TestBit((uint)mEmitterDef.mParticleFlags, (int)ParticleFlags.AlignLaunchSpin))
            {
                aParticle.mSpinPosition = aLaunchAngle;  // 粒子旋转角度对齐发射角度
            }
            else
            {
                aParticle.mSpinPosition = 0f;  // 默认无初始旋转
            }

            aParticle.mSpinVelocity = 0f;
            aParticle.mCrossFadeDuration = 0;
            aParticle.mCrossFadeParticleID = ParticleID.Null;

            ParticleID aParticleID = aDataArray.DataArrayGetID(aParticle);
            mParticleList.AddFirst(aParticleID);
            mParticlesSpawned++;
            UpdateParticle(aParticle);
            return aParticle;
        }

        public bool CrossFadeParticle(TodParticle theParticle, TodParticleEmitter theToEmitter)
        {
            if (theParticle.mCrossFadeDuration > 0)  // 粒子已处于交叉混合的过程中
            {
                Debug.Log(DebugType.Warn, "We don't support cross fading more than one at a time");
                return false;
            }

            if (!Definition.FloatTrackIsSet(theToEmitter.mEmitterDef.mCrossFadeDuration))  // 目标发射器未设定交叉混合时长轨道
            {
                Debug.Log(DebugType.Warn, "Can't cross fade to emitter that doesn't have CrossFadeDuration");
                return false;
            }

            Debug.ASSERT(theToEmitter != this);  // 不能交叉混合至自身

            TodParticle aToParticle = theToEmitter.SpawnParticle(0, 1);
            if (aToParticle == null)
            {
                return false;
            }

            if (mEmitterCrossFadeCountDown > 0)  // 如果源发射器正处于交叉混合过程中
            {
                theParticle.mCrossFadeDuration = mEmitterCrossFadeCountDown;  // 源粒子的交叉混合的时长即为源发射器交叉混合的剩余时长
            }
            else
            {
                float aCrossFadeDurationInterp = RandomNumbers.Rand(1f);
                int aCrossFadeDuration = (int)Definition.FloatTrackEvaluate(theToEmitter.mEmitterDef.mCrossFadeDuration, mSystemTimeValue, aCrossFadeDurationInterp);
                theParticle.mCrossFadeDuration = Math.Max(1, aCrossFadeDuration);  // 随机取得交叉混合的时长（至少 1 帧）
            }

            if (!Definition.FloatTrackIsSet(theToEmitter.mEmitterDef.mParticleDuration))  // 如果目标发射器未定义粒子持续时间
            {
                aToParticle.mParticleDuration = theParticle.mCrossFadeDuration;  // 目标粒子的持续时间等于交叉混合的时间
            }

            aToParticle.mCrossFadeParticleID = mParticleSystem.mParticleHolder.mParticles.DataArrayGetID(theParticle);  // 赋值交叉混合来源的粒子编号
            return true;
        }

        public void CrossFadeEmitter(TodParticleEmitter theToEmitter)
        {
            if (mEmitterCrossFadeCountDown > 0)
            {
                Debug.Log(DebugType.Warn, "We don't support cross fading emitters more than one at a time");
                return;
            }

            if (!Definition.FloatTrackIsSet(theToEmitter.mEmitterDef.mCrossFadeDuration))
            {
                Debug.Log(DebugType.Warn, "Can't cross fade to emitter that doesn't have CrossFadeDuration");
                return;
            }

            Debug.ASSERT(theToEmitter != this);

            float aCrossFadeDurationInterp = RandomNumbers.Rand(1f);
            mEmitterCrossFadeCountDown = (int)Definition.FloatTrackEvaluate(theToEmitter.mEmitterDef.mCrossFadeDuration, mSystemTimeValue, aCrossFadeDurationInterp);
            mEmitterCrossFadeCountDown = Math.Max(1, mEmitterCrossFadeCountDown);
            mCrossFadeEmitterID = mParticleSystem.mParticleHolder.mEmitters.DataArrayGetID(theToEmitter);
            if (!Definition.FloatTrackIsSet(theToEmitter.mEmitterDef.mSystemDuration))
            {
                theToEmitter.mSystemDuration = mEmitterCrossFadeCountDown;
            }

            for (LinkedListNode<ParticleID> aNode = mParticleList.First; aNode != null; aNode = aNode.Next)
            {
                CrossFadeParticle(mParticleSystem.mParticleHolder.mParticles.DataArrayGet(aNode.Value), theToEmitter);
            }
        }

        public bool CrossFadeParticleToName(TodParticle theParticle, string theEmitterName)
        {
            TodEmitterDefinition aDef = mParticleSystem.FindEmitterDefByName(theEmitterName);
            if (aDef == null)
            {
                Debug.Log(DebugType.Warn, $"Can't find emitter to cross fade: {theEmitterName}");
                return false;
            }

            if (mParticleSystem.mParticleHolder.mEmitters.mSize == mParticleSystem.mParticleHolder.mEmitters.mMaxSize)
            {
                Debug.Log(DebugType.Warn, "Too many emitters to cross fade");
                return false;
            }

            TodParticleEmitter aEmitter = mParticleSystem.mParticleHolder.mEmitters.DataArrayAlloc();
            aEmitter.TodEmitterInitialize(mSystemCenter.X, mSystemCenter.Y, mParticleSystem, aDef);
            ParticleEmitterID aEmitterID = mParticleSystem.mParticleHolder.mEmitters.DataArrayGetID(aEmitter);
            mParticleSystem.mEmitterList.AddLast(aEmitterID);
            return CrossFadeParticle(theParticle, aEmitter);
        }

        public void DeleteAll()
        {
            while (mParticleList.Count != 0)
            {
                ParticleID anId = mParticleList.First.Value;
                mParticleList.RemoveFirst();
                DataArray<TodParticle, ParticleID> aDataArray = mParticleSystem.mParticleHolder.mParticles;
                aDataArray.DataArrayFree(aDataArray.DataArrayGet(anId));
            }
        }

        public void UpdateParticleField(TodParticle theParticle, ParticleField theParticleField,
            float theParticleTimeValue, int theFieldIndex)
        {
            Debug.ASSERT(theFieldIndex < TodLibConstants.MAX_PARTICLE_FIELDS);
            float aInterpX = theParticle.mParticleFieldInterp[theFieldIndex][0];
            float aInterpY = theParticle.mParticleFieldInterp[theFieldIndex][1];
            float x = Definition.FloatTrackEvaluate(theParticleField.mX, theParticleTimeValue, aInterpX);
            float y = Definition.FloatTrackEvaluate(theParticleField.mY, theParticleTimeValue, aInterpY);
            
            switch (theParticleField.mFieldType)
            {
            case ParticleFieldType.Invalid:
            case ParticleFieldType.SystemPosition:
                break;
            case ParticleFieldType.Friction:  // 摩擦力场
                theParticle.mVelocity.X *= 1f - x;
                theParticle.mVelocity.Y *= 1f - y;
                break;
            case ParticleFieldType.Acceleration:  // 加速度场
                theParticle.mVelocity.X += x * 0.01f;
                theParticle.mVelocity.Y += y * 0.01f;
                break;
            case ParticleFieldType.Attractor:  // 弹性力场
            {
                float aDiffX = x - (theParticle.mPosition.X - mSystemCenter.X);
                float aDiffY = y - (theParticle.mPosition.Y - mSystemCenter.Y);
                // 加速度的方向始终从粒子所在位置指向“标准位置”
                theParticle.mVelocity.X += aDiffX * 0.01f;
                theParticle.mVelocity.Y += aDiffY * 0.01f;
                break;
            }
            case ParticleFieldType.MaxVelocity:  // 限速场
                theParticle.mVelocity.X = TodCommon.ClampFloat(theParticle.mVelocity.X, -x, x);
                theParticle.mVelocity.Y = TodCommon.ClampFloat(theParticle.mVelocity.Y, -y, y);
                break;
            case ParticleFieldType.Velocity:  // 匀速场
                theParticle.mPosition.X += x * 0.01f;
                theParticle.mPosition.Y += y * 0.01f;
                break;
            case ParticleFieldType.Position:  // 定位场
            {
                float aLastX = TodParticleGlobal.FloatTrackEvaluateFromLastTime(theParticleField.mX, theParticle.mParticleLastTimeValue, aInterpX);
                float aLastY = TodParticleGlobal.FloatTrackEvaluateFromLastTime(theParticleField.mY, theParticle.mParticleLastTimeValue, aInterpY);
                theParticle.mPosition.X += x - aLastX;
                theParticle.mPosition.Y += y - aLastY;
                break;
            }
            case ParticleFieldType.GroundConstraint:
                if (theParticle.mPosition.Y >= mSystemCenter.Y + y)  // 判断是否触及地面
                {
                    theParticle.mPosition.Y = mSystemCenter.Y + y;  // 将坐标重置至地面
                    float aCollisionReflect = Definition.FloatTrackEvaluate(mEmitterDef.mCollisionReflect, theParticleTimeValue, theParticle.mParticleInterp[(int)ParticleTracks.ParticleCollisionReflect]);
                    float aCollisionSpin = Definition.FloatTrackEvaluate(mEmitterDef.mCollisionSpin, theParticleTimeValue, theParticle.mParticleInterp[(int)ParticleTracks.ParticleCollisionSpin]) / 1000f;
                    theParticle.mSpinVelocity = theParticle.mVelocity.Y * aCollisionSpin;
                    theParticle.mVelocity.X *= aCollisionReflect;
                    theParticle.mVelocity.Y *= -aCollisionReflect;
                }

                break;
            case ParticleFieldType.Shake:  // 震动
            {
                float aLastX = TodParticleGlobal.FloatTrackEvaluateFromLastTime(theParticleField.mX, theParticle.mParticleLastTimeValue, aInterpX);
                float aLastY = TodParticleGlobal.FloatTrackEvaluateFromLastTime(theParticleField.mY, theParticle.mParticleLastTimeValue, aInterpY);
                // 先恢复上一次震动效果的影响
                int aLastRandSeed = theParticle.mParticleAge - 1;
                if (aLastRandSeed == -1)
                {
                    aLastRandSeed = theParticle.mParticleDuration - 1;
                }
                static float Next(ref int theSeed)
                {
                    long next = theSeed * 0x5DEECE66DL + 0xBL;
                    theSeed = (int)(next & 0x7FFFFFFFL);
                    return (next & 0x7FFF) / (float)0x7FFF;
                }
                int aSeed = aLastRandSeed * RuntimeHelpers.GetHashCode(theParticle);
                theParticle.mPosition.X -= aLastX * (Next(ref aSeed) * 2f - 1f);
                theParticle.mPosition.Y -= aLastY * (Next(ref aSeed) * 2f - 1f);
                aSeed = theParticle.mParticleAge * RuntimeHelpers.GetHashCode(theParticle);
                theParticle.mPosition.X += x * (Next(ref aSeed) * 2f - 1f);
                theParticle.mPosition.Y += y * (Next(ref aSeed) * 2f - 1f);
                break;
            }
            case ParticleFieldType.Circle:  // 圆周
            {
                Vector2 aToCenter = theParticle.mPosition - mSystemCenter;
                Vector2 aMotion = Vector2.Normalize(aToCenter.GetPerp());  // 标准化的法向量
                float aRadius = aToCenter.Length();
                aMotion *= 0.01f * (x + aRadius * y);
                theParticle.mPosition += aMotion;
                break;
            }
            case ParticleFieldType.Away:  // 远离
            {
                Vector2 aToCenter = theParticle.mPosition - mSystemCenter;
                Vector2 aMotion = Vector2.Normalize(aToCenter);  // 标准化的方向向量
                float aRadius = aToCenter.Length();
                aMotion *= 0.01f * (x + aRadius * y);
                theParticle.mPosition += aMotion;
                break;
            }
            default:
                Debug.ASSERT(false);
                break;
            }
        }

        public void UpdateSystemField(ParticleField theParticleField, float theParticleTimeValue, int theFieldIndex)
        {
            Debug.ASSERT(theFieldIndex < TodLibConstants.MAX_PARTICLE_FIELDS);
            float aInterpX = mSystemFieldInterp[theFieldIndex][0];
            float aInterpY = mSystemFieldInterp[theFieldIndex][1];
            float x = Definition.FloatTrackEvaluate(theParticleField.mX, theParticleTimeValue, aInterpX);
            float y = Definition.FloatTrackEvaluate(theParticleField.mY, theParticleTimeValue, aInterpY);
            
            switch (theParticleField.mFieldType)
            {
            case ParticleFieldType.SystemPosition:
                float aLastX = TodParticleGlobal.FloatTrackEvaluateFromLastTime(theParticleField.mX, mSystemLastTimeValue, aInterpX);
                float aLastY = TodParticleGlobal.FloatTrackEvaluateFromLastTime(theParticleField.mY, mSystemLastTimeValue, aInterpY);
                mSystemCenter.X += x - aLastX;
                mSystemCenter.Y += y - aLastY;
                break;
            default:
                Debug.ASSERT(false);
                break;
            }
        }

        public float SystemTrackEvaluate(FloatParameterTrack theTrack, ParticleSystemTracks theSystemTrack)
        {
            return Definition.FloatTrackEvaluate(theTrack, mSystemTimeValue, mTrackInterp[(int)theSystemTrack]);
        }

        public static float ParticleTrackEvaluate(FloatParameterTrack theTrack, TodParticle theParticle, ParticleTracks theParticleTrack)
        {
            return Definition.FloatTrackEvaluate(theTrack, theParticle.mParticleTimeValue, theParticle.mParticleInterp[(int)theParticleTrack]);
        }

        public void DeleteParticle(TodParticle theParticle)
        {
            TodParticle aCrossFadeParticle = mParticleSystem.mParticleHolder.mParticles.DataArrayTryToGet(theParticle.mCrossFadeParticleID);
            if (aCrossFadeParticle != null)
            {
                aCrossFadeParticle.mParticleEmitter.DeleteParticle(aCrossFadeParticle);  // 同时删除交叉混合的源粒子
                theParticle.mCrossFadeParticleID = ParticleID.Null;
            }

            ParticleID aParticleID = mParticleSystem.mParticleHolder.mParticles.DataArrayGetID(theParticle);
            mParticleList.Remove(aParticleID);
            mParticleSystem.mParticleHolder.mParticles.DataArrayFree(theParticle);
        }

        public void DeleteNonCrossFading()
        {
            for (LinkedListNode<ParticleID> aNode = mParticleList.First; aNode != null;)
            {
                TodParticle aParticle = mParticleSystem.mParticleHolder.mParticles.DataArrayGet(aNode.Value);
                aNode = aNode.Next;
                if (aParticle.mCrossFadeDuration <= 0)  // 当粒子不处于交叉混合状态，则删除该粒子
                {
                    DeleteParticle(aParticle);
                }
            }
        }
    }
}
