using System.Collections.Generic;

namespace EffectViewer.EffectRuntime.Particle
{
    public class ParticleSystem : IDataArrayItem
    {
        public string mEffectType;
        public ParticleDefinition mParticleDef;
        public ParticleHolder mParticleHolder;
        public readonly LinkedList<ParticleEmitterID> mEmitterList = [];
        public bool mDead;
        public bool mIsAttachment;
        public int mRenderOrder;
        public bool mDontUpdate;

        uint IDataArrayItem.Id { get; set; }
        int IDataArrayItem.Index { get; init; }

        public ParticleSystem()
        {
            Reset();
        }

        public void Reset()
        {
            mEffectType = null;
            mParticleDef = null;
            mParticleHolder = null;
            mDead = false;
            mDontUpdate = false;
            mIsAttachment = false;
            mRenderOrder = 0;
            mEmitterList.Clear();
        }

        public void Dispose()
        {
            ParticleSystemDie();
            mEmitterList.Clear();
        }

        public void InitializeFromDefinition(float theX, float theY, int theRenderOrder, ParticleDefinition theDefinition, string theEffectType)
        {
            Debug.Assert(mParticleHolder != null);
            mParticleDef = theDefinition;
            mRenderOrder = theRenderOrder;
            mEffectType = theEffectType;

            for (int i = 0; i < theDefinition.mEmitterDefCount; i++)
            {
                ParticleEmitterDefinition aDef = theDefinition.mEmitterDefs[i];
                if (!Definition.FloatTrackIsSet(aDef.mCrossFadeDuration))
                {
                    if (EffectUtility.TestBit((uint)aDef.mParticleFlags, (int)ParticleFlags.DieIfOverloaded) &&
                        mParticleHolder.IsOverLoaded())
                    {
                        ParticleSystemDie();
                        break;
                    }

                    ParticleEmitter aEmitter = mParticleHolder.mEmitters.DataArrayAlloc();
                    aEmitter.InitializeEmitter(theX, theY, this, aDef);
                    mEmitterList.AddLast(mParticleHolder.mEmitters.DataArrayGetID(aEmitter));
                }
            }
        }

        public void ParticleSystemDie()
        {
            for (LinkedListNode<ParticleEmitterID> aNode = mEmitterList.First; aNode != null; aNode = aNode.Next)
            {
                ParticleEmitter aEmitter = mParticleHolder.mEmitters.DataArrayGet(aNode.Value);
                aEmitter.DeleteAll();
                mParticleHolder.mEmitters.DataArrayFree(aEmitter);
            }

            mEmitterList.Clear();
            mDead = true;
        }

        public void Update()
        {
            if (!mDontUpdate)
            {
                bool aEmitterAlive = false;
                for (LinkedListNode<ParticleEmitterID> aNode = mEmitterList.First; aNode != null; aNode = aNode.Next)
                {
                    ParticleEmitter aEmitter = mParticleHolder.mEmitters.DataArrayGet(aNode.Value);
                    aEmitter.Update();
                    if (Definition.FloatTrackIsSet(aEmitter.mEmitterDef.mCrossFadeDuration) &&
                        aEmitter.mParticleList.Count > 0 ||
                        !aEmitter.mDead)
                    {
                        aEmitterAlive = true;
                    }
                }

                if (!aEmitterAlive)
                {
                    mDead = true;
                }
            }
        }

        public void Draw(EffectViewer.EffectRuntime.Graphics.Graphics g)
        {
            for (LinkedListNode<ParticleEmitterID> aNode = mEmitterList.First; aNode != null; aNode = aNode.Next)
            {
                mParticleHolder.mEmitters.DataArrayGet(aNode.Value).Draw(g);
            }
        }

        public void SystemMove(float theX, float theY)
        {
            for (LinkedListNode<ParticleEmitterID> aNode = mEmitterList.First; aNode != null; aNode = aNode.Next)
            {
                mParticleHolder.mEmitters.DataArrayGet(aNode.Value).SystemMove(theX, theY);
            }
        }

        public void OverrideColor(string theEmitterName, EffectColor theColor)
        {
            for (LinkedListNode<ParticleEmitterID> aNode = mEmitterList.First; aNode != null; aNode = aNode.Next)
            {
                ParticleEmitter aEmitter = mParticleHolder.mEmitters.DataArrayGet(aNode.Value);
                if (theEmitterName == null ||
                    string.Compare(theEmitterName, aEmitter.mEmitterDef.mName, true) == 0)
                {
                    aEmitter.mColorOverride = theColor;
                }
            }
        }

        public void OverrideExtraAdditiveDraw(string theEmitterName, bool theEnableExtraAdditiveDraw)
        {
            for (LinkedListNode<ParticleEmitterID> aNode = mEmitterList.First; aNode != null; aNode = aNode.Next)
            {
                ParticleEmitter aEmitter = mParticleHolder.mEmitters.DataArrayGet(aNode.Value);
                if (theEmitterName == null ||
                    string.Compare(theEmitterName, aEmitter.mEmitterDef.mName, true) == 0)
                {
                    aEmitter.mExtraAdditiveDrawOverride = theEnableExtraAdditiveDraw;
                }
            }
        }

        public void OverrideImage(string theEmitterName, Image theImage)
        {
            for (LinkedListNode<ParticleEmitterID> aNode = mEmitterList.First; aNode != null; aNode = aNode.Next)
            {
                ParticleEmitter aEmitter = mParticleHolder.mEmitters.DataArrayGet(aNode.Value);
                if (theEmitterName == null ||
                    string.Compare(theEmitterName, aEmitter.mEmitterDef.mName, true) == 0)
                {
                    aEmitter.mImageOverride = theImage;
                }
            }
        }

        public void OverrideFrame(string theEmitterName, int theFrame)
        {
            for (LinkedListNode<ParticleEmitterID> aNode = mEmitterList.First; aNode != null; aNode = aNode.Next)
            {
                ParticleEmitter aEmitter = mParticleHolder.mEmitters.DataArrayGet(aNode.Value);
                if (theEmitterName == null ||
                    string.Compare(theEmitterName, aEmitter.mEmitterDef.mName, true) == 0)
                {
                    aEmitter.mFrameOverride = theFrame;
                }
            }
        }

        public void OverrideScale(string theEmitterName, float theScale)
        {
            for (LinkedListNode<ParticleEmitterID> aNode = mEmitterList.First; aNode != null; aNode = aNode.Next)
            {
                ParticleEmitter aEmitter = mParticleHolder.mEmitters.DataArrayGet(aNode.Value);
                if (theEmitterName == null ||
                    string.Compare(theEmitterName, aEmitter.mEmitterDef.mName, true) == 0)
                {
                    aEmitter.mScaleOverride = theScale;
                }
            }
        }

        public void CrossFade(string theEmitterName)
        {
            ParticleEmitterDefinition aEmitterDef = FindEmitterDefByName(theEmitterName);
            if (aEmitterDef == null)
            {
                Debug.Log(DebugType.Error, $"Can't find cross fade emitter: {theEmitterName}");
                return;
            }

            if (!Definition.FloatTrackIsSet(aEmitterDef.mCrossFadeDuration))
            {
                Debug.Log(DebugType.Error, $"Can't cross fade without duration set: {theEmitterName}");
                return;
            }

            if (mParticleHolder.mEmitters.mSize + mEmitterList.Count > mParticleHolder.mEmitters.mMaxSize)
            {
                Debug.Log(DebugType.Warn, "Too many emitters to cross fade");
                ParticleSystemDie();
                return;
            }

            for (LinkedListNode<ParticleEmitterID> aNode = mEmitterList.First; aNode != null; aNode = aNode.Next)
            {
                ParticleEmitter aEmitter = mParticleHolder.mEmitters.DataArrayGet(aNode.Value);
                if (aEmitter.mEmitterDef != aEmitterDef)  // 不能交叉混合至同种类的发射器
                {
                    ParticleEmitter aCrossFadeEmitter = mParticleHolder.mEmitters.DataArrayAlloc();
                    aCrossFadeEmitter.InitializeEmitter(aEmitter.mSystemCenter.X, aEmitter.mSystemCenter.Y, this, aEmitterDef);
                    ParticleEmitterID aCrossFadeEmitterID = mParticleHolder.mEmitters.DataArrayGetID(aCrossFadeEmitter);
                    mEmitterList.AddLast(aCrossFadeEmitterID);
                    aEmitter.CrossFadeEmitter(aCrossFadeEmitter);
                }
            }
        }

        public ParticleEmitter FindEmitterByName(string theEmitterName)
        {
            for (LinkedListNode<ParticleEmitterID> aNode = mEmitterList.First; aNode != null; aNode = aNode.Next)
            {
                ParticleEmitter aEmitter = mParticleHolder.mEmitters.DataArrayGet(aNode.Value);
                if (string.Compare(theEmitterName, aEmitter.mEmitterDef.mName, true) == 0)
                {
                    return aEmitter;
                }
            }

            return null;
        }

        public ParticleEmitterDefinition FindEmitterDefByName(string theEmitterName)
        {
            for (int i = 0; i < mParticleDef.mEmitterDefCount; i++)
            {
                ParticleEmitterDefinition aEmitterDef = mParticleDef.mEmitterDefs[i];
                if (string.Compare(theEmitterName, aEmitterDef.mName, true) == 0)
                {
                    return aEmitterDef;
                }
            }

            return null;
        }
    }
}
